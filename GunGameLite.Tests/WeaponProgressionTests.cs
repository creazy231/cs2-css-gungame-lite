using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class WeaponProgressionTests
{
    [Theory]
    [InlineData(31, null, true, 0)]   // no other players -> start at 0
    [InlineData(31, 0, true, 0)]      // pack at 0 -> start 0
    [InlineData(31, 10, true, 9)]     // late joiner starts one behind the pack tail
    [InlineData(31, 10, false, 0)]    // catch-up disabled -> always 0
    [InlineData(31, 40, true, 30)]    // clamps to last index
    public void StartIndex_RespectsCatchUpAndBounds(int count, int? minOthers, bool catchUp, int expected)
        => Assert.Equal(expected, WeaponProgression.StartIndex(count, minOthers, catchUp));

    [Theory]
    [InlineData(30, 31, true)]
    [InlineData(29, 31, false)]
    [InlineData(0, 1, true)]
    public void IsWinningKill_TrueOnlyWhenHoldingLastWeapon(int cur, int count, bool expected)
        => Assert.Equal(expected, WeaponProgression.IsWinningKill(cur, count));

    [Theory]
    [InlineData(5, 31, null, true, 6)]   // solo: linear +1
    [InlineData(5, 31, 6, true, 6)]      // ahead of tail-1: linear +1
    [InlineData(3, 31, 10, true, 10)]    // straggler snaps up to pack tail
    [InlineData(3, 31, 10, false, 4)]    // catch-up off: linear +1
    [InlineData(10, 31, 3, true, 11)]    // leader: linear +1
    public void AdvanceIndex_LinearOrSnaps(int cur, int count, int? minOthers, bool catchUp, int expected)
        => Assert.Equal(expected, WeaponProgression.AdvanceIndex(cur, count, minOthers, catchUp));

    [Theory]
    [InlineData(5, 4)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void DemoteIndex_FloorsAtZero(int cur, int expected)
        => Assert.Equal(expected, WeaponProgression.DemoteIndex(cur));
}
