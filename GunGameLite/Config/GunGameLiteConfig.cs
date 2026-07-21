using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace GunGameLite.Config;

public sealed class Colors
{
    [JsonPropertyName("Highlight")] public string Highlight { get; set; } = "{PURPLE}";
}

public sealed class BotsConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("QuotaPerTeam")] public int QuotaPerTeam { get; set; } = 2;
    [JsonPropertyName("Difficulty")] public int Difficulty { get; set; } = 3;
}

public sealed class WarmupConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = false;
    [JsonPropertyName("Seconds")] public int Seconds { get; set; } = 30;
}

public sealed class SpawnProtectionConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("Seconds")] public float Seconds { get; set; } = 2.0f;
    [JsonPropertyName("DropOnAttack")] public bool DropOnAttack { get; set; } = true;
}

public sealed class CatchUpConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
}

public sealed class GameplayConfig
{
    [JsonPropertyName("Warmup")] public WarmupConfig Warmup { get; set; } = new();
    [JsonPropertyName("SpawnProtection")] public SpawnProtectionConfig SpawnProtection { get; set; } = new();
    [JsonPropertyName("InfiniteAmmo")] public bool InfiniteAmmo { get; set; } = true;
    [JsonPropertyName("InfiniteReserve")] public bool InfiniteReserve { get; set; } = true;
    [JsonPropertyName("GrenadeRegiveDelaySeconds")] public float GrenadeRegiveDelaySeconds { get; set; } = 3.0f;
    [JsonPropertyName("CatchUp")] public CatchUpConfig CatchUp { get; set; } = new();
    [JsonPropertyName("TeamKillsCount")] public bool TeamKillsCount { get; set; } = false;
    [JsonPropertyName("KnifeStealsLevel")] public bool KnifeStealsLevel { get; set; } = true;
    [JsonPropertyName("DemoteOnSuicide")] public bool DemoteOnSuicide { get; set; } = true;
}

public sealed class HudConfig
{
    [JsonPropertyName("ShowProgress")] public bool ShowProgress { get; set; } = true;
}

public sealed class GunGameLiteConfig : BasePluginConfig
{
    public override int Version => 2;

    [JsonPropertyName("Tag")] public string Tag { get; set; } = "{DEFAULT}[{PURPLE}GGL{DEFAULT}]{DEFAULT} ";
    [JsonPropertyName("Colors")] public Colors Colors { get; set; } = new();
    [JsonPropertyName("Bots")] public BotsConfig Bots { get; set; } = new();
    [JsonPropertyName("Gameplay")] public GameplayConfig Gameplay { get; set; } = new();
    [JsonPropertyName("Hud")] public HudConfig Hud { get; set; } = new();
    [JsonPropertyName("WeaponOrder")] public string[] WeaponOrder { get; set; } = (string[])DefaultWeaponOrder.Clone();

    /// <summary>The classic GunGame Lite progression (rifles/snipers/LMGs -> SMGs/shotguns -> scout -> pistols -> HE -> knife).</summary>
    public static readonly string[] DefaultWeaponOrder =
    {
        "awp", "mag7", "g3sg1", "negev", "scar20", "m249",
        "ak47", "m4a1", "sg556", "aug", "galilar", "famas",
        "mac10", "mp9", "bizon", "p90", "sawedoff", "ump45", "xm1014", "mp7", "nova",
        "ssg08",
        "deagle", "elite", "tec9", "hkp2000", "fiveseven", "p250", "glock",
        "hegrenade", "knife",
    };

    /// <summary>Clamp out-of-range values and guarantee a usable weapon order. Call from OnConfigParsed.</summary>
    public void Validate()
    {
        if (Bots.QuotaPerTeam < 0) Bots.QuotaPerTeam = 0;
        Bots.Difficulty = Math.Clamp(Bots.Difficulty, 0, 3);

        if (Gameplay.GrenadeRegiveDelaySeconds < 0f) Gameplay.GrenadeRegiveDelaySeconds = 0f;
        if (Gameplay.SpawnProtection.Seconds < 0f) Gameplay.SpawnProtection.Seconds = 0f;
        if (Gameplay.Warmup.Seconds < 0) Gameplay.Warmup.Seconds = 0;

        if (WeaponOrder is null || WeaponOrder.Length == 0 || WeaponOrder[^1] != "knife")
            WeaponOrder = (string[])DefaultWeaponOrder.Clone();
    }
}
