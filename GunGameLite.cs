using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;

namespace GunGameLite;

public class GunGameLite : BasePlugin, IPluginConfig<GunGameLiteConfig>
{
    public override string ModuleAuthor => "creazy.eth";
    public override string ModuleName => "GunGameLite";
    public override string ModuleDescription =>
        "GunGameLite is a simple and basic GunGame Plugin for CounterStrikeSharp";
    public override string ModuleVersion => "0.0.1";
    
    private string ModuleDisplayName => "GunGame Lite";

    // TODO: Move this into the config
    private readonly string[] _weaponOrder = {
        "awp",
        "mag7",
        "g3sg1",
        "negev",
        "scar20",
        "m249",
        
        "ak47",
        "m4a1",
        "sg556",
        "aug",
        "galilar",
        "famas",
        
        "mac10",
        "mp9",
        "bizon",
        "p90",
        "sawedoff",
        "ump45",
        "xm1014",
        "mp7",
        "nova",
        
        "ssg08",
        
        "deagle",
        "elite",
        "tec9",
        "hkp2000",
        "fiveseven",
        "p250",
        "glock",
        
        "hegrenade",
        "knife",
    };

    public bool RoundActive = false;
    private readonly Dictionary<CCSPlayerController, string> _playerCurrentWeapon = new();
    
    public GunGameLiteConfig Config { get; set; } = new();
    
    private static GunGameLite? _instance;
    public static GunGameLite? Instance
    {
        get { return _instance ??= new GunGameLite(); }
    }
    
    public void OnConfigParsed(GunGameLiteConfig config)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Loading config file");

        Config = config;
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Config file loaded");
    }

    public override void Load(bool hotReload)
    {
        LogHelper.LogToConsole(ConsoleColor.Green,
            $"[GunGame Lite Plugin] -> {ModuleName} version {ModuleVersion} loaded");
        
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
        
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawned);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
    }
    
    private HookResult OnWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Warmup started");
        
        return HookResult.Continue;
    }
    
    private HookResult OnPlayerSpawned(EventPlayerSpawn @event, GameEventInfo info)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Player spawned");
        
        var player = @event.Userid;
        
        if (player == null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;
        
        if (_playerCurrentWeapon.ContainsKey(player))
        {
            _playerCurrentWeapon[player] = "";
        }
        else
        {
            _playerCurrentWeapon.TryAdd(player, "");
        }
        
        LogHelper.LogToChat(player, $"{Config.Tag}Welcome to {Config.Colors.Highlight}{ModuleDisplayName}{ChatColors.Default}, {player.PlayerName}");
        
        GivePlayerNextWeapon(player);
        
        return HookResult.Continue;
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
        // TODO: Move this into a separate file and call it from here
        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");
        
        Server.ExecuteCommand("mp_respawn_immunitytime 3");
        Server.ExecuteCommand("mp_death_drop_gun 0");
        Server.ExecuteCommand("mp_death_drop_grenade 0");
        Server.ExecuteCommand("mp_death_drop_defuser 0");
        Server.ExecuteCommand("mp_free_armor 2");
        Server.ExecuteCommand("cash_player_killed_enemy_default 0");
        Server.ExecuteCommand("cash_player_killed_enemy_factor 0");
        Server.ExecuteCommand("cash_player_killed_teammate 0");
        Server.ExecuteCommand("cash_player_killed_teammate_factor 0");
        Server.ExecuteCommand("mp_maxmoney 0");
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("contributionscore_suicide 0");
        Server.ExecuteCommand("contributionscore_objective_kill 0");
        Server.ExecuteCommand("contributionscore_kill 0");
        Server.ExecuteCommand("contributionscore_assist 0");
        
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
            _playerCurrentWeapon[player] = "";
            GivePlayerNextWeapon(player);
        }
        
        return HookResult.Continue;
    }
    
    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        RoundActive = false;
        
        return HookResult.Continue;
    }

    private void GivePlayerCurrentWeapon(CCSPlayerController player)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Giving player {player.PlayerName} current weapon");
        
        var currentWeapon = _playerCurrentWeapon[player];
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
        var currentWeaponIndex = Array.IndexOf(_weaponOrder, currentWeapon);
        
        // check if currentWeaponIndex is + 1 is out of bounds. restart game if true
        if (currentWeaponIndex + 1 >= _weaponOrder.Length)
        {
            LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{player.PlayerName}{ChatColors.Default} won the game! Announce winner...");
            
            AnnounceWinner(player);
            
            return;
        }
        
        var nextWeapon = _weaponOrder[currentWeaponIndex + 1];
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Giving player {player.PlayerName} next weapon: {nextWeapon}");
        
        player.PawnArmor = 100;
        player.RemoveWeapons();
        player.GiveNamedItem("item_assaultsuit");
        
        if (nextWeapon == "knife")
        {
            // player.GiveNamedItem("weapon_knifegg");
            player.GiveNamedItem("weapon_knife");
            // TODO: Announce that player has knife and is about to win in chat to all players
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
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Someone died. {player.PlayerName} was killed by {attacker.PlayerName}");
        
        if (player.UserId == attacker.UserId) return HookResult.Continue;
        
        attacker.Score++;
        
        // Announce players name with most Score in chat to all players. use name and score
        // TODO: Move this into a separate function and store the highest score and player in a variable
        // TODO: Also call out "has taken the lead" if the player has more score than the previous highest score
        var activePlayers = Utilities.GetPlayers();
        var highestScore = 0;
        var highestScorePlayer = "";
        foreach (var activePlayer in activePlayers.Where(activePlayer => activePlayer.Score > highestScore))
        {
            highestScore = activePlayer.Score;
            highestScorePlayer = activePlayer.PlayerName;
        }
        
        LogHelper.LogToChatAll($"{Config.Tag}{Config.Colors.Highlight}{highestScorePlayer}{ChatColors.Default} is leading with {Config.Colors.Highlight}{highestScore}{ChatColors.Default} kills!");
        
        GivePlayerNextWeapon(attacker);
        
        return HookResult.Continue;
    }
    
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Weapon fired");
        
        var player = @event.Userid;
        
        if (player == null || !player.PawnIsAlive) return HookResult.Continue;
        
        var currentWeapon = _playerCurrentWeapon[player];
        
        if (currentWeapon == "hegrenade")
        {
            Instance?.AddTimer(5.0f, () =>
            {
                GivePlayerCurrentWeapon(player);
            });
        }
        
        return HookResult.Continue;
    }
    
    private void AnnounceWinner(CCSPlayerController winner)
    {
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Announcing winner");
        
        RoundActive = false;
        
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        gameRules.TerminateRound(10.0f, RoundEndReason.RoundDraw);
        
        // Abort Round
        
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");
        
        var activePlayers = Utilities.GetPlayers();
        foreach (var activePlayer in activePlayers)
        {
            activePlayer.RemoveWeapons();
            PrintHelper.PlayerCenterHtml(activePlayer, $"<font class='fontSize-m' color='white'>Winner Winner Chicken Dinner</font><br><font class='fontSize-l' color='#2E9F65'>{winner.PlayerName}</font>", 5);
        }
    }
}
