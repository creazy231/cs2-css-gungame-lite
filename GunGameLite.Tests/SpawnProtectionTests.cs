using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class SpawnProtectionTests
{
    [Fact]
    public void DisabledIsNeverActive()
        => Assert.False(SpawnProtection.IsActive(false, 0, 2, true, false));

    [Fact]
    public void ActiveBeforeDuration()
        => Assert.True(SpawnProtection.IsActive(true, 1.0, 2.0, true, false));

    [Theory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void ExpiresAtOrAfterDuration(double elapsed)
        => Assert.False(SpawnProtection.IsActive(true, elapsed, 2.0, true, false));

    [Fact]
    public void EndsOnAttackWhenDropOnAttack()
        => Assert.False(SpawnProtection.IsActive(true, 0.5, 2.0, dropOnAttack: true, hasAttacked: true));

    [Fact]
    public void AttackIgnoredWhenDropOnAttackOff()
        => Assert.True(SpawnProtection.IsActive(true, 0.5, 2.0, dropOnAttack: false, hasAttacked: true));
}
