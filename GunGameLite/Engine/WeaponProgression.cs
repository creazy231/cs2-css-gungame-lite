namespace GunGameLite.Engine;

/// <summary>Pure weapon-index math for GunGame progression. No CS2 runtime.</summary>
public static class WeaponProgression
{
    /// <summary>Starting index for a (possibly late-joining) player.
    /// <paramref name="minOthers"/> is the lowest index among all other players, or null if none.</summary>
    public static int StartIndex(int weaponCount, int? minOthers, bool catchUp)
    {
        if (weaponCount <= 0) return 0;
        if (!catchUp || minOthers is null) return 0;
        return Clamp(minOthers.Value - 1, 0, weaponCount - 1);
    }

    /// <summary>True when a kill made while holding <paramref name="currentIndex"/> wins the game.</summary>
    public static bool IsWinningKill(int currentIndex, int weaponCount)
        => currentIndex + 1 >= weaponCount;

    /// <summary>Next index after a counted kill. Callers must check <see cref="IsWinningKill"/> first.
    /// A straggler (more than one gun behind the pack tail) snaps up to the tail when catch-up is on.</summary>
    public static int AdvanceIndex(int currentIndex, int weaponCount, int? minOthers, bool catchUp)
    {
        var next = currentIndex + 1;
        if (catchUp && minOthers is not null && next < minOthers.Value - 1)
            next = minOthers.Value;
        return Clamp(next, 0, weaponCount - 1);
    }

    /// <summary>Index after a demotion; floors at 0.</summary>
    public static int DemoteIndex(int currentIndex) => Math.Max(currentIndex - 1, 0);

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;
}
