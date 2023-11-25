using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace GunGameLite;

public class Colors
{
    [JsonPropertyName("Highlight")] public string Highlight { get; set; } = "{PURPLE}";
}

public class Bots
{
    /// <summary>
    /// If bots should be enabled.
    /// </summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The amount of bots to spawn per team.
    /// </summary>
    [JsonPropertyName("QuotaPerTeam")]
    public int QuotaPerTeam { get; set; } = 2;

    /// <summary>
    /// The difficulty of the bots. Can be 0, 1, 2 or 3.
    /// </summary>
    [JsonPropertyName("Difficulty")]
    public int Difficulty { get; set; } = 3;
}

public class GunGameLiteConfig : BasePluginConfig
{
    public override int Version => 1;

    [JsonPropertyName("Tag")] public string Tag { get; set; } = "{DEFAULT}[{PURPLE}GGL{DEFAULT}]{DEFAULT} ";

    [JsonPropertyName("Colors")] public Colors Colors { get; set; } = new Colors();

    [JsonPropertyName("Bots")] public Bots Bots { get; set; } = new Bots();

    [JsonPropertyName("WeaponOrder")]
    public string[] WeaponOrder { get; set; } =
    {
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
}