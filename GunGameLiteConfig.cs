using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace GunGameLite;

public class Colors
{
    [JsonPropertyName("Highlight")]
    public string Highlight { get; set; } = "{PURPLE}";
}

public class GunGameLiteConfig : BasePluginConfig
{
    public override int Version => 1;

    [JsonPropertyName("Tag")]
    public string Tag { get; set; } = "{DEFAULT}[{PURPLE}GGL{DEFAULT}]{DEFAULT} ";

    [JsonPropertyName("Colors")]
    public Colors Colors { get; set; } = new Colors();
}