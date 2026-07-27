namespace GunGameLite.Engine;

/// <summary>How badly the server's game mode conflicts with GunGame.</summary>
public enum GameModeSeverity
{
    /// <summary>Casual — what GunGame Lite is built for. Reported to nobody.</summary>
    Ok,

    /// <summary>Workable, but not the tested configuration. Console only.</summary>
    Notice,

    /// <summary>The mode imposes rules that spoil GunGame. Console + admin chat.</summary>
    Warning,

    /// <summary>The engine runs competing logic that actively breaks GunGame. Console + admin chat.</summary>
    Error,
}

/// <summary>The outcome of classifying a game_type/game_mode pair.</summary>
public readonly record struct GameModeVerdict(GameModeSeverity Severity, string ModeName, string Reason);

/// <summary>
/// Classifies CS2's game_type/game_mode pair. GunGame needs Casual (0/0): it is the only stock mode
/// with neither engine-side weapon progression nor engine-side respawn/loadout logic to fight.
/// Neither cvar can be changed without a map change, so the plugin can only report — never fix.
/// CSS-free so it can be unit tested.
/// </summary>
public static class GameModeCheck
{
    public const int RequiredGameType = 0;
    public const int RequiredGameMode = 0;

    public static GameModeVerdict Evaluate(int gameType, int gameMode) => (gameType, gameMode) switch
    {
        (0, 0) => new GameModeVerdict(GameModeSeverity.Ok, "Casual",
            "Casual is the mode GunGame Lite is built for."),

        (1, 0) => new GameModeVerdict(GameModeSeverity.Error, "Arms Race",
            "Arms Race runs its own weapon ladder, HUD and round-end, which overwrite GunGame Lite's weapon grants."),

        (1, 2) => new GameModeVerdict(GameModeSeverity.Error, "Deathmatch",
            "Deathmatch runs its own respawn and bonus-weapon logic, which fights GunGame Lite's weapon grants."),

        (0, 1) => new GameModeVerdict(GameModeSeverity.Warning, "Competitive",
            "Competitive forces half-time team swaps, friendly fire and match logic, and does not support respawning."),

        (0, 2) => new GameModeVerdict(GameModeSeverity.Warning, "Wingman",
            "Wingman is 2v2 on a small map subset, so bot balancing and the weapon ladder never play out."),

        (2, 0) => new GameModeVerdict(GameModeSeverity.Warning, "Skirmish",
            "Skirmish layers its own round rules on top of GunGame Lite."),

        (4, 0) => new GameModeVerdict(GameModeSeverity.Warning, "Guardian",
            "Guardian is a co-op mission mode with no free-for-all respawn flow."),

        (4, 1) => new GameModeVerdict(GameModeSeverity.Warning, "Co-op Strike",
            "Co-op Strike is a co-op mission mode with no free-for-all respawn flow."),

        (3, 0) => new GameModeVerdict(GameModeSeverity.Notice, "Custom",
            "Custom mode works but is untested and needs a gamemodes_server.txt entry. Casual is recommended."),

        _ => new GameModeVerdict(GameModeSeverity.Warning,
            $"Unknown (game_type {gameType} / game_mode {gameMode})",
            "This game_type/game_mode combination is not recognised and has not been tested with GunGame Lite."),
    };
}
