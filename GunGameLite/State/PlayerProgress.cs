namespace GunGameLite.State;

/// <summary>Per-player game state (CSS-free), keyed by player slot in the engine.</summary>
public sealed class PlayerProgress
{
    public int WeaponIndex { get; set; }
    public int Kills { get; set; }
}
