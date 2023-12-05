using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using GunGameLite.Extensions;

namespace GunGameLite;

public class GunGameLite : BasePlugin, IPluginConfig<GunGameLiteConfig>
{
    public override string ModuleAuthor => "creazy.eth";
    public override string ModuleName => "GunGameLite";
    public override string ModuleDescription =>
        "GunGame Lite is a simple and basic GunGame Plugin for CounterStrikeSharp";
    public override string ModuleVersion => "0.0.1";
    
    private static string ModuleDisplayName => "GunGame Lite";
    
    // TODO: Reset Assists, Kills, Deaths, MVPs on round start
    // TODO: Implement custom spawn protection based on https://github.com/Hackmastr/css-spawn-protection/blob/main/CTSpawnProtection.cs#L12
    
    // Config
    public GunGameLiteConfig Config { get; set; } = new();
    
    
    // Plugin Instance
    public static GunGameLite? Instance { get; private set; }
    
    // Variables
    private readonly Dictionary<CCSPlayerController, string> _playerCurrentWeapon = new();
    private string _highestScoreName = "";
    
    public void OnConfigParsed(GunGameLiteConfig config)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Loading config file");

        Config = config;
        if (config.Bots.Enabled)
        {
            Server.ExecuteCommand("mp_autoteambalance 0");
            Server.ExecuteCommand("mp_limitteams 0");
            
            Server.ExecuteCommand($"bot_difficulty {config.Bots.Difficulty}");
        }
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Config file loaded");
    }

    public override void Load(bool hotReload)
    {
        LogHelper.LogToConsole(ConsoleColor.Green,
            $"[GunGame Lite Plugin] -> {ModuleName} version {ModuleVersion} loaded");
        
        Instance = this;
        
        GunGameLiteServerConfig.LoadServerConfig();
        Server.ExecuteCommand("mp_restartgame 1");
        
        LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{ModuleDisplayName} v{ModuleVersion} loaded!");
        RegisterListeners();
    }

    public override void Unload(bool hotReload)
    {
        LogHelper.LogToConsole(ConsoleColor.Green,
            $"[GunGame Lite Plugin] -> {ModuleName} version {ModuleVersion} unloaded");
    }
    
    private void RegisterListeners()
    {
        RegisterEventHandler<EventRoundAnnounceWarmup>(OnWarmup);
        
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
        
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawned);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventWeaponReload>(OnWeaponReload);
    }
    
    private HookResult OnWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Warmup started");
        
        return HookResult.Continue;
    }
    
    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;
        
        InitializePlayer(@event.Userid);
        
        // var activePlayers = Utilities.GetPlayers();
        // var realPlayers = activePlayers.Where(p => p is { IsValid: true, IsBot: false });
        // LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Real Player joined. Restarting game if there is only one real player.");
        // // if (realPlayers.Count() == 1 && Config.Bots.Enabled) Server.ExecuteCommand("mp_restartgame 1");
        
        return HookResult.Continue;
    }
    
    private HookResult OnPlayerSpawned(EventPlayerSpawn @event, GameEventInfo info)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Player spawned");
        
        var player = @event.Userid;
        if (player == null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;
        
        InitializePlayer(@event.Userid);
        
        return HookResult.Continue;
    }

    private void InitializePlayer(CCSPlayerController player)
    {
        if (_playerCurrentWeapon.ContainsKey(player))
        {
            player.Score = 0;
            _playerCurrentWeapon[player] = "";
        }
        else
        {
            player.Score = 0;
            _playerCurrentWeapon.TryAdd(player, "");
        }
        
        LogHelper.LogToChat(player, $"{Config.Tag}Welcome to {Config.Colors.Highlight}{ModuleDisplayName}{ChatColors.Default}, {player.PlayerName}");
        LogHelper.LogToChat(player, $"{Config.Tag}{Config.Colors.Highlight}{ModuleDisplayName}{ChatColors.Default} is still in development.");
        LogHelper.LogToChat(player, $"{Config.Tag}Please report any bugs to {ChatColors.Gold}creazy231{ChatColors.Default} on Discord!");
        LogHelper.LogToChat(player, $"{Config.Tag}Version: {ChatColors.Gold}{ModuleVersion}{ChatColors.Default}");
        
        GivePlayerNextWeapon(player);
    }
    
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Player spawn");
        
        var player = @event.Userid;
        
        if (player == null || !player.IsValid) return HookResult.Continue;
        
        GivePlayerCurrentWeapon(player);
        
        return HookResult.Continue;
    }
    
    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        GunGameLiteServerConfig.LoadServerConfig();
        Server.ExecuteCommand("bot_kick all");
        
        _playerCurrentWeapon.Clear();

        var weaponEntities = Utilities.FindAllEntitiesByDesignerName<CWeaponBaseItem>("weapon_");
        foreach (var weaponEntity in weaponEntities)
        {
            weaponEntity.Remove();
        }

        var activePlayers = Utilities.GetPlayers();
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Round started with {activePlayers.Count} players");
        
        foreach (var player in activePlayers.Where(player => player is { PawnIsAlive: true }))
        {
            player.Score = 0;

            // Initialize player for the new round
            InitializePlayer(player);
        }
        
        Instance?.AddTimer(0.1f, BotManager.SpawnBots);
        
        return HookResult.Continue;
    }
    
    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _playerCurrentWeapon.Clear();
        
        return HookResult.Continue;
    }

    private void GivePlayerCurrentWeapon(CCSPlayerController player)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Giving player {player.PlayerName} current weapon");
        
        // try to get current weapon from dictionary
        if (!_playerCurrentWeapon.TryGetValue(player, out var currentWeapon)) return;
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Current weapon is {currentWeapon}");
        
        // wait for next tick to give player current weapon
        Instance?.AddTimer(0.1f, () =>
        {
            player.RemoveWeapons();
            player.GiveNamedItem("item_assaultsuit");
            player.GiveNamedItem("weapon_knife");
            player.GiveNamedItem($"weapon_{currentWeapon}");
        });
    }
    
    private void GivePlayerNextWeapon(CCSPlayerController player)
    {
        
        // check if player is in dictionary, otherwise add him with empty weapon
        _playerCurrentWeapon.TryAdd(player, "");

        var currentWeapon = _playerCurrentWeapon[player];
        var currentWeaponIndex = Array.IndexOf(Config.WeaponOrder, currentWeapon);
        
        // check if currentWeaponIndex is + 1 is out of bounds. restart game if true
        if (currentWeaponIndex + 1 >= Config.WeaponOrder.Length)
        {
            LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Player {player.PlayerName} has won the game");
            
            AnnounceWinner(player);
            
            return;
        }
        
        var nextWeapon = Config.WeaponOrder[currentWeaponIndex + 1];
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Giving player {player.PlayerName} next weapon: {nextWeapon}");
        
        player.PawnArmor = 100;
        player.RemoveWeapons();
        player.GiveNamedItem("item_assaultsuit");
        
        if (nextWeapon == "knife")
        {
            // player.GiveNamedItem("weapon_knifegg");
            player.GiveNamedItem("weapon_knife");
            LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{player.PlayerName}{ChatColors.LightRed} has a knife and is about to win!");
        }
        else
        {
            player.GiveNamedItem("weapon_knife");
            player.GiveNamedItem($"weapon_{nextWeapon}");
        }
        
        _playerCurrentWeapon[player] = nextWeapon;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        var attacker = @event.Attacker;
        
        if (player == null || attacker == null) return HookResult.Continue;
        
        // Check if the player was killed by a knife or killed themselves
        if (@event.Weapon == "worldent" || @event.Weapon == "knife")
        {
            // Decrease the player's score by 1
            player.Score--;

            // Move the player back one weapon in the weapon order
            var currentWeapon = _playerCurrentWeapon[player];
            var currentWeaponIndex = Array.IndexOf(Config.WeaponOrder, currentWeapon);
            if (currentWeaponIndex > 0)
            {
                var previousWeapon = Config.WeaponOrder[currentWeaponIndex - 1];
                _playerCurrentWeapon[player] = previousWeapon;
                
                // Announce in chat that the player has been demoted and also announce the reason therefor
                LogHelper.LogToChatAll($"{Config.Tag}{ChatColors.Gold}{player.PlayerName}{ChatColors.Darkred} has been demoted!");
            }
        }
        
        Instance?.AddTimer(0.1f, BotManager.SpawnBots);
        
        if (!attacker.IsValid) return HookResult.Continue;
        attacker.Score++;
        GivePlayerNextWeapon(attacker);
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Someone died. {player.PlayerName} was killed by {attacker.PlayerName}");

        AnnouncePlayerWithMostScore();

        return HookResult.Continue;
    }
    
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        
        if (player == null || !player.PawnIsAlive) return HookResult.Continue;
        
        if (!_playerCurrentWeapon.TryGetValue(player, out var currentWeapon)) return HookResult.Continue;
        
        if (currentWeapon == "hegrenade")
        {
            // TODO: make this variable using config
            Instance?.AddTimer(3.0f, () =>
            {
                GivePlayerCurrentWeapon(player);
            });
        }
        
        return HookResult.Continue;
    }
    
    private void AnnounceWinner(CCSPlayerController winner)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Announcing winner");
        
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");
        
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        gameRules.TerminateRound(10.0f, RoundEndReason.RoundDraw);
        
        var activePlayers = Utilities.GetPlayers();
        foreach (var activePlayer in activePlayers)
        {
            if (activePlayer.IsBot || !activePlayer.IsValid) continue;
            activePlayer.RemoveWeapons();
            PrintHelper.PlayerCenterHtml(activePlayer, $"<font class='fontSize-m' color='white'>Winner Winner Chicken Dinner</font><br><font class='fontSize-l' color='#2E9F65'>{winner.PlayerName}</font>", 5);
        }
    }

    private void AnnouncePlayerWithMostScore()
    {
        var activePlayers = Utilities.GetPlayers();
        var highestScore = 0;
        var highestScoreName = "";
        foreach (var activePlayer in activePlayers.Where(activePlayer => activePlayer.Score > highestScore))
        {
            highestScore = activePlayer.Score;
            highestScoreName = activePlayer.PlayerName;
        }

        if (highestScoreName == _highestScoreName) return;
        
        _highestScoreName = highestScoreName;
        
        LogHelper.LogToChatAll($"{Config.Tag}{ChatColors.Gold}{highestScoreName}{ChatColors.LightRed} has taken the lead with {ChatColors.Gold}{highestScore}{ChatColors.LightRed} kills!");
    }

    #region Events

    [GameEventHandler(HookMode.Post)]
    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo _)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;
        
        _playerCurrentWeapon.Remove(player);
        if (!player.IsBot) Instance?.AddTimer(0.1f, BotManager.SpawnBots);
        
        return HookResult.Continue;
    }

    #endregion
    
    private static HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Player {player.PlayerName} reloaded");

        // Restock player's ammo
        var activeWeapon = player.ActiveWeapon();
        if (activeWeapon == null) return HookResult.Continue;
        activeWeapon.Clip1 = 250;
        activeWeapon.ReserveAmmo[0] = 250;

        return HookResult.Continue;
    }
    
    // Commands
    [RequiresPermissions("@css/root")]
    [ConsoleCommand("ggl_rr", "Restarts the round")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRestartRoundCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null) return;
        Server.ExecuteCommand("mp_restartgame 1");
        LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller.PlayerName}{ChatColors.Default} has restarted the round!");
    }
    
    [RequiresPermissions("@css/root")]
    [ConsoleCommand("ggl_bots", "Toggles bots")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnToggleBotsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null) return;
        
        Config.Bots.Enabled = !Config.Bots.Enabled;
        LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller.PlayerName}{ChatColors.Default} has toggled bots to {Config.Bots.Enabled}!");
        Server.ExecuteCommand("mp_restartgame 1");
    }
    
    [RequiresPermissions("@css/root")]
    [ConsoleCommand("ggl_rl", "Reloads the plugin")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnReloadPluginCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null) return;
        
        Server.ExecuteCommand("css_plugins reload GunGameLite");
        
        LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller.PlayerName}{ChatColors.Default} has reloaded the plugin!");
    }
    
    [RequiresPermissions("@css/root")]
    [ConsoleCommand("ggl_test")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnTestCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null) return;
        AnnounceWinner(caller);
    }
}
