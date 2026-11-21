using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using GunGameLite.Config;
using GunGameLite.Engine;
using GunGameLite.Extensions;
using GunGameLite.Helpers;
using Microsoft.Extensions.Logging;

namespace GunGameLite;

[MinimumApiVersion(371)]
public class GunGameLitePlugin : BasePlugin, IPluginConfig<GunGameLiteConfig>
{
    public override string ModuleAuthor => "creazy.eth";
    public override string ModuleName => "GunGameLite";
    public override string ModuleDescription => "A simple GunGame game mode for Counter-Strike 2.";
    public override string ModuleVersion => "0.2.1";

    public static GunGameLitePlugin? Instance { get; private set; }
    public GunGameLiteConfig Config { get; set; } = new();

    private GunGameEngine _engine = new(GunGameLiteConfig.DefaultWeaponOrder.Length);
    private readonly LeaderTracker _leader = new();
    private readonly HashSet<int> _greeted = new();
    private readonly HashSet<int> _protected = new();
    private bool _warmupActive;
    private GameModeVerdict? _gameModeVerdict;
    private bool _gameModeReported;

    /// <summary>Extra 0.1s retries while a spawning pawn comes up, before giving up on it.</summary>
    private const int MaxPawnReadyAttempts = 3;

    public void OnConfigParsed(GunGameLiteConfig config)
    {
        config.Validate();
        Config = config;
        _engine = new GunGameEngine(
            config.WeaponOrder.Length,
            config.Gameplay.CatchUp.Enabled,
            config.Gameplay.KnifeStealsLevel,
            config.Gameplay.DemoteOnSuicide,
            config.Gameplay.TeamKillsAdvance);

        // mp_autoteambalance / mp_limitteams now live in ServerRuleset and apply unconditionally.
        if (config.Bots.Enabled)
            Server.ExecuteCommand($"bot_difficulty {config.Bots.Difficulty}");
    }

    public override void Load(bool hotReload)
    {
        Instance = this;
        ServerRuleset.Apply(Config);

        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _gameModeReported = false; // new map, report again
            ReportGameMode();
            if (Config.Gameplay.Warmup.Enabled) StartWarmup();
        });

        // Safety net for hot-reload and for boots where the plugin loads after OnMapStart fired.
        // _gameModeReported makes whichever path runs second a no-op.
        AddTimer(1.0f, ReportGameMode);

        Server.ExecuteCommand("mp_restartgame 1");
        Logger.LogInformation("{Name} v{Version} loaded", ModuleName, ModuleVersion);

        // On a cold boot CSS loads plugins before the game server exists: there is no map, no entity
        // and nobody connected. Calling into the game at that point killed the server (issue #1), so a
        // cold boot stops here and the OnMapStart listener above starts the warmup once a map is up.
        // Only a hot reload, with a map already running, carries on.
        if (!hotReload) return;

        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}GunGame Lite v{ModuleVersion} loaded!");

        foreach (var player in Utilities.GetPlayers())
        {
            _engine.OnPlayerJoin(player.Slot);
            if (!player.IsBot) _greeted.Add(player.Slot);
        }

        if (Config.Gameplay.Warmup.Enabled) StartWarmup();
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("{Name} unloaded", ModuleName);
    }

    // ---- Player lifecycle ----

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid) return HookResult.Continue;

        _engine.OnPlayerJoin(player.Slot);
        if (!player.IsBot && _greeted.Add(player.Slot)) Greet(player);

        // Deferred so it lands after the greeting, and so CSS has finished loading admin data.
        // Humans only: bots churn every round via bot_add, and a timer each would be wasted work.
        if (player.IsBot) return HookResult.Continue;

        var slot = player.Slot;
        AddTimer(2.0f, () =>
        {
            var verdict = _gameModeVerdict;
            if (verdict is null) return;
            if (verdict.Value.Severity is not (GameModeSeverity.Warning or GameModeSeverity.Error)) return;

            var p = Utilities.GetPlayerFromSlot(slot);
            if (p is not null && IsWarnableAdmin(p)) SendGameModeWarning(p, verdict.Value);
        });

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        // Do NOT gate on PawnIsAlive here: EventPlayerSpawn fires before the pawn is initialised, so
        // it is usually false at this point. Gating on it skipped the weapon give entirely, leaving
        // the player on the engine's default loadout — a bare knife. Both calls below defer and
        // re-check the pawn themselves, which is the right place for that check.
        if (player is null || !player.IsValid || player.IsHLTV) return HookResult.Continue;

        _engine.OnPlayerJoin(player.Slot);
        GiveWeaponForIndex(player, _engine.GetProgress(player.Slot).WeaponIndex);
        ApplySpawnProtection(player);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid) return HookResult.Continue;
        _engine.OnPlayerJoin(player.Slot); // idempotent: registers late joiners without resetting
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null) return HookResult.Continue;

        _engine.RemovePlayer(player.Slot);
        _greeted.Remove(player.Slot);
        _protected.Remove(player.Slot);
        if (!player.IsBot) AddTimer(0.1f, ApplyBotBalance);
        return HookResult.Continue;
    }

    // ---- Core loop ----

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (victim is null || !victim.IsValid) return HookResult.Continue;

        AddTimer(0.1f, ApplyBotBalance);

        if (_warmupActive) return HookResult.Continue;

        var victimSlot = victim.Slot;
        var isSuicide = attacker is null || !attacker.IsValid
                        || attacker.Slot == victimSlot
                        || @event.Weapon is "worldent" or "world";
        var isKnife = @event.Weapon == "knife";
        var sameTeam = !isSuicide && attacker!.Team == victim.Team;
        var attackerSlot = isSuicide ? victimSlot : attacker!.Slot;

        var result = _engine.OnKill(attackerSlot, victimSlot, sameTeam, isKnife, isSuicide);

        if (result.VictimDemoted)
        {
            victim.Score = _engine.GetProgress(victimSlot).Kills;
            GiveWeaponForIndex(victim, result.VictimNewIndex);
            ChatHelper.ToChatAll($"{Config.Tag}{ChatColors.Gold}{victim.PlayerName}{ChatColors.DarkRed} has been demoted!");
        }

        if (!isSuicide && attacker!.IsValid)
        {
            attacker.Score = _engine.GetProgress(attackerSlot).Kills;

            if (result.AttackerWon)
            {
                AnnounceWinner(attacker);
            }
            else if (result.AttackerAdvanced)
            {
                GiveWeaponForIndex(attacker, result.AttackerNewIndex);
                if (Config.WeaponOrder[result.AttackerNewIndex] == "knife") AnnounceAboutToWin(attacker);
                UpdateLeader(attacker.PlayerName);
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ServerRuleset.Apply(Config);
        Server.ExecuteCommand("bot_kick all");

        _engine.Reset();
        _leader.Reset();
        _protected.Clear();

        foreach (var weapon in Utilities.FindAllEntitiesByDesignerName<CWeaponBaseItem>("weapon_"))
            weapon.Remove();

        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, PawnIsAlive: true }))
        {
            player.Score = 0;
            _engine.OnPlayerJoin(player.Slot);
            GiveWeaponForIndex(player, _engine.GetProgress(player.Slot).WeaponIndex);
        }

        AddTimer(0.1f, ApplyBotBalance);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _engine.Reset();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;

        if (Config.Gameplay.SpawnProtection.DropOnAttack) ClearSpawnProtection(player.Slot);

        if (!_engine.Players.TryGetValue(player.Slot, out var progress)) return HookResult.Continue;
        if (progress.WeaponIndex < 0 || progress.WeaponIndex >= Config.WeaponOrder.Length) return HookResult.Continue;

        var weapon = Config.WeaponOrder[progress.WeaponIndex];
        if (weapon is "hegrenade" or "molotov" or "incgrenade")
        {
            var slot = player.Slot;
            AddTimer(Config.Gameplay.GrenadeRegiveDelaySeconds, () =>
            {
                var p = Utilities.GetPlayerFromSlot(slot);
                // Re-read the current index at timer time: a grenade kill may have advanced
                // the thrower (e.g. to the knife); don't overwrite it with a stale grenade.
                if (p is { IsValid: true, PawnIsAlive: true })
                    GiveWeaponForIndex(p, _engine.GetProgress(slot).WeaponIndex);
            });
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
    {
        if (!Config.Gameplay.InfiniteAmmo) return HookResult.Continue;

        var player = @event.Userid;
        if (player is null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;

        var weapon = player.ActiveWeapon();
        if (weapon is null) return HookResult.Continue;

        weapon.Clip1 = 250;
        if (Config.Gameplay.InfiniteReserve) weapon.ReserveAmmo[0] = 250;
        return HookResult.Continue;
    }

    // ---- Game mode check ----

    /// <summary>
    /// Reads game_type/game_mode. Returns null when either cvar is missing, is not an int (CSS throws
    /// InvalidOperationException on a type mismatch, so a future CS2 changing their type must not break
    /// map start), or reads outside its plausible range: staying quiet beats telling an admin their
    /// mode is "Unknown (game_type 1919885362)".
    /// </summary>
    private static (int gameType, int gameMode)? ReadGameMode()
    {
        // Two locals, not ?. — GetPrimitiveValue<T> returns a ref, which cannot be null-lifted.
        var typeCvar = ConVar.Find("game_type");
        var modeCvar = ConVar.Find("game_mode");
        if (typeCvar is null || modeCvar is null) return null;

        int gameType, gameMode;
        try
        {
            gameType = typeCvar.GetPrimitiveValue<int>();
            gameMode = modeCvar.GetPrimitiveValue<int>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (gameType is < 0 or > 8 || gameMode is < 0 or > 4) return null;

        return (gameType, gameMode);
    }

    /// <summary>
    /// Logs the game mode verdict once per map and chats it to admins. game_type/game_mode only take
    /// effect on a map change, so reporting is all the plugin can do — it must not try to fix them.
    /// </summary>
    private void ReportGameMode()
    {
        if (!Config.Server.GameModeCheck || _gameModeReported) return;
        _gameModeReported = true;

        var read = ReadGameMode();
        if (read is null)
        {
            Logger.LogDebug("game_type/game_mode unreadable; skipping the game mode check");
            return;
        }

        var (gameType, gameMode) = read.Value;
        var verdict = GameModeCheck.Evaluate(gameType, gameMode);
        _gameModeVerdict = verdict;

        switch (verdict.Severity)
        {
            case GameModeSeverity.Ok:
                Logger.LogInformation("Game mode {ModeName} (game_type {GameType} / game_mode {GameMode}) is correct for GunGame",
                    verdict.ModeName, gameType, gameMode);
                return;

            case GameModeSeverity.Notice:
                Logger.LogInformation("Game mode {ModeName} (game_type {GameType} / game_mode {GameMode}): {Reason}",
                    verdict.ModeName, gameType, gameMode, verdict.Reason);
                return;

            case GameModeSeverity.Warning:
                Logger.LogWarning("Wrong game mode {ModeName} (game_type {GameType} / game_mode {GameMode}): {Reason} Fix: restart with +game_type {WantType} +game_mode {WantMode} (Casual), then change map.",
                    verdict.ModeName, gameType, gameMode, verdict.Reason, GameModeCheck.RequiredGameType, GameModeCheck.RequiredGameMode);
                break;

            case GameModeSeverity.Error:
                Logger.LogError("Wrong game mode {ModeName} (game_type {GameType} / game_mode {GameMode}): {Reason} Fix: restart with +game_type {WantType} +game_mode {WantMode} (Casual), then change map.",
                    verdict.ModeName, gameType, gameMode, verdict.Reason, GameModeCheck.RequiredGameType, GameModeCheck.RequiredGameMode);
                break;
        }

        foreach (var admin in Utilities.GetPlayers().Where(IsWarnableAdmin))
            SendGameModeWarning(admin, verdict);
    }

    /// <summary>A connected human admin who should be told about a bad game mode.</summary>
    private static bool IsWarnableAdmin(CCSPlayerController? player)
        => player is { IsValid: true, IsBot: false, IsHLTV: false }
           && (AdminManager.PlayerHasPermissions(player, "@css/generic")
               || AdminManager.PlayerHasPermissions(player, "@css/root"));

    private void SendGameModeWarning(CCSPlayerController admin, GameModeVerdict verdict)
    {
        ChatHelper.ToChat(admin, $"{Config.Tag}{ChatColors.DarkRed}Wrong game mode: {ChatColors.Gold}{verdict.ModeName}");
        ChatHelper.ToChat(admin, $"{Config.Tag}{verdict.Reason}");
        ChatHelper.ToChat(admin, $"{Config.Tag}Fix: restart the server with {ChatColors.Gold}+game_type {GameModeCheck.RequiredGameType} +game_mode {GameModeCheck.RequiredGameMode}{ChatColors.Default} (Casual), then change map.");
    }

    // ---- Helpers ----

    private void Greet(CCSPlayerController player)
    {
        ChatHelper.ToChat(player, $"{Config.Tag}Welcome to {Config.Colors.Highlight}GunGame Lite{ChatColors.Default}, {player.PlayerName}!");
        ChatHelper.ToChat(player, $"{Config.Tag}First to knife-kill wins. Every kill upgrades your weapon.");
        ChatHelper.ToChat(player, $"{Config.Tag}Version: {ChatColors.Gold}{ModuleVersion}{ChatColors.Default}");
    }

    /// <summary>
    /// Runs an action once the player's pawn is actually up. EventPlayerSpawn fires before the pawn is
    /// initialised, so anything touching it has to wait — and has to retry rather than give up, or the
    /// player is left on the engine's default loadout (a bare knife), which is how bots ended up
    /// knife-only. Re-resolves from the slot each attempt per the repo's slot-keyed convention.
    /// </summary>
    private void WhenPawnReady(int slot, Action<CCSPlayerController> action, int attempt = 0)
    {
        AddTimer(0.1f, () =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is null || !player.IsValid) return;

            if (!player.PawnIsAlive)
            {
                if (attempt < MaxPawnReadyAttempts) WhenPawnReady(slot, action, attempt + 1);
                return;
            }

            action(player);
        });
    }

    private void GiveWeaponForIndex(CCSPlayerController player, int index)
    {
        if (index < 0 || index >= Config.WeaponOrder.Length) return;
        var weapon = Config.WeaponOrder[index];

        WhenPawnReady(player.Slot, p =>
        {
            p.RemoveWeapons();
            p.GiveNamedItem("item_assaultsuit");
            p.GiveNamedItem("weapon_knife");
            if (weapon != "knife") p.GiveNamedItem($"weapon_{weapon}");
        });
    }

    private void AnnounceAboutToWin(CCSPlayerController player)
    {
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{player.PlayerName}{ChatColors.LightRed} has a knife and is about to win!");
        foreach (var p in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
            HudHelper.PlayerCenterHtml(p,
                $"<font class='fontSize-m' color='white'><span color='#de9b35'>{player.PlayerName}</span><br>has a knife and is about to win!</font>", 3);
    }

    private void AnnounceWinner(CCSPlayerController winner)
    {
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        gameRules?.TerminateRound(5.0f, RoundEndReason.RoundDraw);

        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid))
        {
            p.RemoveWeapons();
            if (!p.IsBot)
                HudHelper.PlayerCenterHtml(p,
                    $"<font class='fontSize-m' color='white'>Winner Winner Chicken Dinner</font><br><font class='fontSize-l' color='#2E9F65'>{winner.PlayerName}</font>", 5);
        }

        ChatHelper.ToChatAll($"{Config.Tag}Winner Winner Chicken Dinner: {ChatColors.Gold}{winner.PlayerName}{ChatColors.Default}!");
    }

    private void UpdateLeader(string scorerName)
    {
        var scores = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.IsOnATeam())
            .Select(p => (p.PlayerName, p.Score));

        var top = LeaderTracker.FindTop(scores);
        if (top is null) return;

        var message = _leader.Update(top.Value.name, top.Value.kills, scorerName);
        switch (message.Change)
        {
            case LeaderChange.TookLead:
                var from = message.PreviousLeaderName is null
                    ? ""
                    : $"from {ChatColors.Gold}{message.PreviousLeaderName}{ChatColors.LightRed} ";
                ChatHelper.ToChatAll($"{Config.Tag}{ChatColors.Gold}{message.LeaderName}{ChatColors.LightRed} has taken the lead {from}with {ChatColors.Gold}{message.Kills}{ChatColors.LightRed} points!");
                break;
            case LeaderChange.StillLeading:
                ChatHelper.ToChatAll($"{Config.Tag}{ChatColors.Gold}{message.LeaderName}{ChatColors.LightRed} is still leading with {ChatColors.Gold}{message.Kills}{ChatColors.LightRed} points!");
                break;
        }
    }

    private void ApplyBotBalance()
    {
        int humansT = 0, botsT = 0, humansCt = 0, botsCt = 0;
        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && p.IsOnATeam()))
        {
            var t = p.IsTerrorist();
            if (p.IsBot)
            {
                if (t) botsT++; else botsCt++;
            }
            else
            {
                if (t) humansT++; else humansCt++;
            }
        }

        var balance = BotBalancer.Compute(humansT, botsT, humansCt, botsCt, Config.Bots.QuotaPerTeam, Config.Bots.Enabled);
        for (var i = 0; i < balance.AddT; i++) Server.ExecuteCommand("bot_add t");
        for (var i = 0; i < balance.AddCt; i++) Server.ExecuteCommand("bot_add ct");
        for (var i = 0; i < balance.KickT; i++) Server.ExecuteCommand("bot_kick t");
        for (var i = 0; i < balance.KickCt; i++) Server.ExecuteCommand("bot_kick ct");
    }

    private void ApplySpawnProtection(CCSPlayerController player)
    {
        if (!Config.Gameplay.SpawnProtection.Enabled) return;

        // Same reason as the weapon give: reading the pawn on the spawn tick used to silently skip
        // protection for exactly the players whose pawn was slowest to come up.
        WhenPawnReady(player.Slot, p =>
        {
            var pawn = p.PlayerPawn.Value;
            if (pawn is null || !pawn.IsValid) return;

            pawn.TakesDamage = false;
            _protected.Add(p.Slot);

            AddTimer(Config.Gameplay.SpawnProtection.Seconds, () => ClearSpawnProtection(p.Slot));
        });
    }

    private void ClearSpawnProtection(int slot)
    {
        if (!_protected.Remove(slot)) return;
        var pawn = Utilities.GetPlayerFromSlot(slot)?.PlayerPawn.Value;
        if (pawn is not null && pawn.IsValid) pawn.TakesDamage = true;
    }

    private void StartWarmup()
    {
        if (!Config.Gameplay.Warmup.Enabled || _warmupActive) return;
        _warmupActive = true;
        ChatHelper.ToChatAll($"{Config.Tag}Warmup started for {Config.Gameplay.Warmup.Seconds}s — kills don't count yet.");
        AddTimer(Config.Gameplay.Warmup.Seconds, () =>
        {
            _warmupActive = false;
            Server.ExecuteCommand("mp_restartgame 1");
            ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}GunGame starts now!");
        });
    }

    // ---- Admin commands ----

    [ConsoleCommand("css_ggl_restart", "Restarts the GunGame")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRestartCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.ExecuteCommand("mp_restartgame 1");
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller?.PlayerName ?? "Console"}{ChatColors.Default} restarted the game!");
    }

    [ConsoleCommand("css_ggl_bots", "Toggles bots")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnToggleBotsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Config.Bots.Enabled = !Config.Bots.Enabled;
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller?.PlayerName ?? "Console"}{ChatColors.Default} toggled bots to {Config.Bots.Enabled}!");
        AddTimer(0.1f, ApplyBotBalance);
    }

    [ConsoleCommand("css_ggl_reload", "Reloads the plugin")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnReloadCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.ExecuteCommand("css_plugins reload GunGameLite");
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller?.PlayerName ?? "Console"}{ChatColors.Default} reloaded the plugin!");
    }

    [ConsoleCommand("css_ggl_check", "Reports the server game mode and the cvars GunGame Lite enforces")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnCheckCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var read = ReadGameMode();
        if (read is null)
        {
            info.ReplyToCommand($"{Config.Tag}Could not read game_type/game_mode.".ReplaceTags());
            return;
        }

        var (gameType, gameMode) = read.Value;
        var verdict = GameModeCheck.Evaluate(gameType, gameMode);
        var colour = verdict.Severity == GameModeSeverity.Ok ? ChatColors.Green : ChatColors.DarkRed;

        info.ReplyToCommand($"{Config.Tag}Game mode: {colour}{verdict.ModeName}{ChatColors.Default} (game_type {gameType} / game_mode {gameMode})".ReplaceTags());

        if (verdict.Severity != GameModeSeverity.Ok)
        {
            info.ReplyToCommand($"{Config.Tag}{verdict.Reason}".ReplaceTags());
            info.ReplyToCommand($"{Config.Tag}Required: {ChatColors.Gold}+game_type {GameModeCheck.RequiredGameType} +game_mode {GameModeCheck.RequiredGameMode}{ChatColors.Default} (Casual)".ReplaceTags());
        }

        info.ReplyToCommand(FormattableString.Invariant(
            $"{Config.Tag}Enforcing: mp_freezetime {Config.Server.FreezeTimeSeconds}, mp_roundtime {Config.Server.RoundTimeMinutes}, mp_randomspawn {(Config.Server.RandomSpawns ? 1 : 0)}, spec_freeze_time {Config.Server.DeathCamSeconds}").ReplaceTags());
    }
}
