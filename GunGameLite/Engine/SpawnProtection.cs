namespace GunGameLite.Engine;

/// <summary>Pure spawn-protection timing policy. The plugin owns the actual invulnerability
/// (pawn.TakesDamage) and clock; this only decides whether protection should still hold.</summary>
public static class SpawnProtection
{
    public static bool IsActive(bool enabled, double elapsedSeconds, double durationSeconds, bool dropOnAttack, bool hasAttacked)
    {
        if (!enabled) return false;
        if (dropOnAttack && hasAttacked) return false;
        return elapsedSeconds < durationSeconds;
    }
}
