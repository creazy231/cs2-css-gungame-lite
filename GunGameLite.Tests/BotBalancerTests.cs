using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class BotBalancerTests
{
    [Fact]
    public void FillsEmptyTeamsToQuota()
    {
        var b = BotBalancer.Compute(0, 0, 0, 0, quotaPerTeam: 2, botsEnabled: true);
        Assert.Equal(new BotBalance(2, 2, 0, 0), b);
    }

    [Fact]
    public void OneHumanNeedsOneBot()
    {
        var b = BotBalancer.Compute(1, 0, 0, 0, 2, true);
        Assert.Equal(2, b.AddCt);
        Assert.Equal(1, b.AddT);
    }

    [Fact]
    public void HumansAtQuotaAddNothing()
        => Assert.Equal(new BotBalance(0, 0, 0, 0), BotBalancer.Compute(2, 0, 2, 0, 2, true));

    [Fact]
    public void OverQuotaHumansAreNotKicked()
    {
        var b = BotBalancer.Compute(3, 0, 0, 0, 2, true);
        Assert.Equal(0, b.AddT);
        Assert.Equal(0, b.KickT);
    }

    [Fact]
    public void ExtraBotsAreKicked()
    {
        var b = BotBalancer.Compute(0, 3, 0, 0, 2, true);
        Assert.Equal(1, b.KickT);
        Assert.Equal(0, b.AddT);
    }

    [Fact]
    public void DisabledKicksAllBots()
    {
        var b = BotBalancer.Compute(0, 2, 1, 3, 2, botsEnabled: false);
        Assert.Equal(new BotBalance(0, 0, 2, 3), b);
    }
}
