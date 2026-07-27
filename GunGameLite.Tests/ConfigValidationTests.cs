using GunGameLite.Config;
using Xunit;

namespace GunGameLite.Tests;

public class ConfigValidationTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var c = new GunGameLiteConfig();
        Assert.Equal(3, c.Version);
        Assert.Equal(2, c.Bots.QuotaPerTeam);
        Assert.True(c.Gameplay.InfiniteAmmo);
        Assert.False(c.Gameplay.TeamKillsCount);
        Assert.Equal("knife", c.WeaponOrder[^1]);
        Assert.Equal(31, c.WeaponOrder.Length);
    }

    [Fact]
    public void Validate_ClampsNumericRanges()
    {
        var c = new GunGameLiteConfig();
        c.Bots.QuotaPerTeam = -5;
        c.Bots.Difficulty = 9;
        c.Gameplay.GrenadeRegiveDelaySeconds = -1f;
        c.Gameplay.SpawnProtection.Seconds = -2f;
        c.Gameplay.Warmup.Seconds = -3;

        c.Validate();

        Assert.Equal(0, c.Bots.QuotaPerTeam);
        Assert.Equal(3, c.Bots.Difficulty);
        Assert.Equal(0f, c.Gameplay.GrenadeRegiveDelaySeconds);
        Assert.Equal(0f, c.Gameplay.SpawnProtection.Seconds);
        Assert.Equal(0, c.Gameplay.Warmup.Seconds);
    }

    [Fact]
    public void Validate_DifficultyBelowRangeClampsToZero()
    {
        var c = new GunGameLiteConfig();
        c.Bots.Difficulty = -1;
        c.Validate();
        Assert.Equal(0, c.Bots.Difficulty);
    }

    [Fact]
    public void Validate_EmptyWeaponOrder_FallsBackToDefault()
    {
        var c = new GunGameLiteConfig { WeaponOrder = Array.Empty<string>() };
        c.Validate();
        Assert.Equal(GunGameLiteConfig.DefaultWeaponOrder, c.WeaponOrder);
    }

    [Fact]
    public void Validate_WeaponOrderNotEndingInKnife_FallsBackToDefault()
    {
        var c = new GunGameLiteConfig { WeaponOrder = new[] { "ak47", "awp" } };
        c.Validate();
        Assert.Equal("knife", c.WeaponOrder[^1]);
    }

    [Fact]
    public void Validate_ValidCustomWeaponOrder_IsPreserved()
    {
        var custom = new[] { "glock", "deagle", "knife" };
        var c = new GunGameLiteConfig { WeaponOrder = custom };
        c.Validate();
        Assert.Equal(custom, c.WeaponOrder);
    }

    [Fact]
    public void ServerDefaults_AreSane()
    {
        var c = new GunGameLiteConfig();
        Assert.True(c.Server.GameModeCheck);
        Assert.True(c.Server.RandomSpawns);
        Assert.Equal(0, c.Server.FreezeTimeSeconds);
        Assert.Equal(60, c.Server.RoundTimeMinutes);
        Assert.Equal(0f, c.Server.DeathCamSeconds);
    }

    [Fact]
    public void Validate_ClampsServerRanges()
    {
        var c = new GunGameLiteConfig();
        c.Server.FreezeTimeSeconds = -5;
        c.Server.RoundTimeMinutes = 0;
        c.Server.DeathCamSeconds = -1.5f;

        c.Validate();

        Assert.Equal(0, c.Server.FreezeTimeSeconds);
        Assert.Equal(1, c.Server.RoundTimeMinutes);
        Assert.Equal(0f, c.Server.DeathCamSeconds);
    }

    [Fact]
    public void Validate_LeavesValidServerValuesAlone()
    {
        var c = new GunGameLiteConfig();
        c.Server.FreezeTimeSeconds = 3;
        c.Server.RoundTimeMinutes = 15;
        c.Server.DeathCamSeconds = 2.5f;
        c.Server.RandomSpawns = false;
        c.Server.GameModeCheck = false;

        c.Validate();

        Assert.Equal(3, c.Server.FreezeTimeSeconds);
        Assert.Equal(15, c.Server.RoundTimeMinutes);
        Assert.Equal(2.5f, c.Server.DeathCamSeconds);
        Assert.False(c.Server.RandomSpawns);
        Assert.False(c.Server.GameModeCheck);
    }
}
