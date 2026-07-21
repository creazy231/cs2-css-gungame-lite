namespace GunGameLite.Engine;

/// <summary>What the plugin must enact in the world after a kill.</summary>
public sealed class KillResult
{
    public bool AttackerAdvanced { get; set; }
    public int AttackerNewIndex { get; set; }
    public bool AttackerWon { get; set; }
    public bool VictimDemoted { get; set; }
    public int VictimNewIndex { get; set; }
}
