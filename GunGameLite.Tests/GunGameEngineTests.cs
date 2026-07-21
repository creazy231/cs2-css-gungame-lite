using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class GunGameEngineTests
{
    private static GunGameEngine New(int count = 31) => new(count);

    [Fact]
    public void OnPlayerJoin_FirstPlayerStartsAtZero_AndIsIdempotent()
    {
        var e = New();
        Assert.Equal(0, e.OnPlayerJoin(0));
        e.Seed(0, 5);
        Assert.Equal(5, e.OnPlayerJoin(0)); // already registered -> no reset
    }

    [Fact]
    public void OnPlayerJoin_LateJoinerStartsNearPack()
    {
        var e = New();
        e.Seed(0, 10);
        e.Seed(1, 10);
        Assert.Equal(9, e.OnPlayerJoin(2));
    }

    [Fact]
    public void OnKill_EnemyNormal_AdvancesAttacker_VictimUnchanged()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: false, isSuicide: false);
        Assert.True(r.AttackerAdvanced);
        Assert.Equal(6, r.AttackerNewIndex);
        Assert.False(r.VictimDemoted);
        Assert.Equal(1, e.GetProgress(0).Kills);
        Assert.Equal(5, e.GetProgress(1).WeaponIndex);
    }

    [Fact]
    public void OnKill_EnemyKnife_AdvancesAttacker_DemotesVictim()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: true, isSuicide: false);
        Assert.True(r.AttackerAdvanced);
        Assert.True(r.VictimDemoted);
        Assert.Equal(4, r.VictimNewIndex);
        Assert.Equal(4, e.GetProgress(1).WeaponIndex);
    }

    [Fact]
    public void OnKill_Suicide_DemotesVictim_NoAdvance()
    {
        var e = New();
        e.Seed(0, 5);
        var r = e.OnKill(0, 0, sameTeam: false, weaponIsKnife: false, isSuicide: true);
        Assert.False(r.AttackerAdvanced);
        Assert.True(r.VictimDemoted);
        Assert.Equal(4, e.GetProgress(0).WeaponIndex);
    }

    [Fact]
    public void OnKill_TeamKill_IsNoOpByDefault()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: true, weaponIsKnife: false, isSuicide: false);
        Assert.False(r.AttackerAdvanced);
        Assert.False(r.VictimDemoted);
        Assert.Equal(5, e.GetProgress(0).WeaponIndex);
        Assert.Equal(0, e.GetProgress(0).Kills);
    }

    [Fact]
    public void OnKill_TeamKill_CountsWhenConfigured()
    {
        var e = new GunGameEngine(31, teamKillsCount: true);
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: true, weaponIsKnife: false, isSuicide: false);
        Assert.True(r.AttackerAdvanced);
        Assert.Equal(6, r.AttackerNewIndex);
    }

    [Fact]
    public void OnKill_WinningKill_SetsWon_NoAdvance()
    {
        var e = New(31);
        e.Seed(0, 30);
        e.Seed(1, 30);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: false, isSuicide: false);
        Assert.True(r.AttackerWon);
        Assert.False(r.AttackerAdvanced);
    }

    [Fact]
    public void OnKill_StragglerSnapsUpToPack()
    {
        var e = New();
        e.Seed(1, 10);
        e.OnPlayerJoin(0); // starts near pack, but seed to force a straggler
        e.Seed(0, 0);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: false, isSuicide: false);
        Assert.Equal(10, r.AttackerNewIndex);
    }

    [Fact]
    public void OnKill_UnknownSlots_DoNotThrow()
    {
        var e = New();
        var ex = Record.Exception(() => e.OnKill(7, 8, false, false, false));
        Assert.Null(ex);
        Assert.Equal(1, e.GetProgress(7).Kills);
    }

    [Fact]
    public void Reset_ClearsAllPlayers()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 3);
        e.Reset();
        Assert.Empty(e.Players);
    }

    [Fact]
    public void SoloRun_ReachesWin()
    {
        var e = New(5); // 5 weapons: win on the kill made at index 4
        e.OnPlayerJoin(0);
        e.Seed(1, 0); // static victim keeps advance linear
        KillResult r = new();
        for (var i = 0; i < 5; i++)
            r = e.OnKill(0, 1, false, false, false);
        Assert.True(r.AttackerWon);
    }

    [Fact]
    public void OnKill_EnemyKnife_AttackerCatchUpUsesPreDemotionPack()
    {
        // A straggler knifing a pack member should snap up to the SAME index as an
        // equivalent normal kill (10), not one less because the victim just dropped to 9.
        var e = New();
        e.Seed(1, 10);
        e.OnPlayerJoin(0);
        e.Seed(0, 0);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: true, isSuicide: false);
        Assert.Equal(10, r.AttackerNewIndex);
        Assert.Equal(9, e.GetProgress(1).WeaponIndex);
    }

    [Fact]
    public void OnKill_Demotion_DecrementsVictimKillsFlooredAtZero()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5, kills: 3);
        e.OnKill(0, 1, sameTeam: false, weaponIsKnife: true, isSuicide: false);
        Assert.Equal(2, e.GetProgress(1).Kills);
        Assert.Equal(4, e.GetProgress(1).WeaponIndex);
    }

    [Fact]
    public void OnKill_Demotion_VictimKillsNeverGoBelowZero()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5, kills: 0);
        e.OnKill(0, 1, sameTeam: false, weaponIsKnife: true, isSuicide: false);
        Assert.Equal(0, e.GetProgress(1).Kills);
    }
}
