using GunGameLite.State;

namespace GunGameLite.Engine;

/// <summary>The CSS-free core of GunGame Lite. Owns per-slot progression and kill outcomes.
/// Works only in integers, so it runs in unit tests with no CS2 server.</summary>
public sealed class GunGameEngine
{
    private readonly Dictionary<int, PlayerProgress> _players = new();
    private readonly int _weaponCount;
    private readonly bool _catchUp;
    private readonly bool _knifeStealsLevel;
    private readonly bool _demoteOnSuicide;
    private readonly bool _teamKillsCount;

    public GunGameEngine(int weaponCount, bool catchUp = true, bool knifeStealsLevel = true,
                         bool demoteOnSuicide = true, bool teamKillsCount = false)
    {
        _weaponCount = Math.Max(1, weaponCount);
        _catchUp = catchUp;
        _knifeStealsLevel = knifeStealsLevel;
        _demoteOnSuicide = demoteOnSuicide;
        _teamKillsCount = teamKillsCount;
    }

    public IReadOnlyDictionary<int, PlayerProgress> Players => _players;

    /// <summary>Returns the player's stored progress, or a fresh default for unknown slots (never throws).</summary>
    public PlayerProgress GetProgress(int slot)
        => _players.TryGetValue(slot, out var p) ? p : new PlayerProgress();

    /// <summary>Register a player if new (assigning a catch-up start); returns their current weapon index.
    /// Idempotent — existing players are not reset.</summary>
    public int OnPlayerJoin(int slot)
    {
        if (_players.TryGetValue(slot, out var existing)) return existing.WeaponIndex;
        var start = WeaponProgression.StartIndex(_weaponCount, MinIndexExcept(slot), _catchUp);
        _players[slot] = new PlayerProgress { WeaponIndex = start };
        return start;
    }

    /// <summary>Directly set a player's progress (used for round setup and tests).</summary>
    public void Seed(int slot, int weaponIndex, int kills = 0)
        => _players[slot] = new PlayerProgress
        {
            WeaponIndex = Math.Clamp(weaponIndex, 0, _weaponCount - 1),
            Kills = Math.Max(0, kills)
        };

    public KillResult OnKill(int attackerSlot, int victimSlot, bool sameTeam, bool weaponIsKnife, bool isSuicide)
    {
        var result = new KillResult();
        var victim = Ensure(victimSlot);

        if (isSuicide)
        {
            if (_demoteOnSuicide) DemoteVictim(victim, result);
            return result; // suicide never advances anyone
        }

        if (sameTeam && !_teamKillsCount) return result; // team kill: no-op

        var attacker = Ensure(attackerSlot);

        // Snapshot the pack minimum BEFORE any demotion so the attacker's catch-up
        // target does not depend on whether this same kill demoted a pack member.
        var attackerMinOthers = MinIndexExcept(attackerSlot);

        if (weaponIsKnife && _knifeStealsLevel) DemoteVictim(victim, result);

        attacker.Kills += 1;

        if (WeaponProgression.IsWinningKill(attacker.WeaponIndex, _weaponCount))
        {
            result.AttackerWon = true;
            result.AttackerNewIndex = attacker.WeaponIndex;
            return result;
        }

        attacker.WeaponIndex = WeaponProgression.AdvanceIndex(
            attacker.WeaponIndex, _weaponCount, attackerMinOthers, _catchUp);
        result.AttackerAdvanced = true;
        result.AttackerNewIndex = attacker.WeaponIndex;
        return result;
    }

    public void RemovePlayer(int slot) => _players.Remove(slot);

    public void Reset() => _players.Clear();

    private static void DemoteVictim(PlayerProgress victim, KillResult result)
    {
        victim.WeaponIndex = WeaponProgression.DemoteIndex(victim.WeaponIndex);
        victim.Kills = Math.Max(0, victim.Kills - 1);
        result.VictimDemoted = true;
        result.VictimNewIndex = victim.WeaponIndex;
    }

    private PlayerProgress Ensure(int slot)
    {
        if (_players.TryGetValue(slot, out var p)) return p;
        p = new PlayerProgress();
        _players[slot] = p;
        return p;
    }

    private int? MinIndexExcept(int slot)
    {
        int? min = null;
        foreach (var (key, value) in _players)
        {
            if (key == slot) continue;
            if (min is null || value.WeaponIndex < min) min = value.WeaponIndex;
        }
        return min;
    }
}
