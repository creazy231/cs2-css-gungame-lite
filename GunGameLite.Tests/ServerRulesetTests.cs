using System.Globalization;
using GunGameLite.Config;
using GunGameLite.Helpers;
using Xunit;

namespace GunGameLite.Tests;

public class ServerRulesetTests
{
    private static IReadOnlyList<string> CommandsFor(Action<GunGameLiteConfig>? configure = null)
    {
        var config = new GunGameLiteConfig();
        configure?.Invoke(config);
        return ServerRuleset.Commands(config);
    }

    /// <summary>The cvar a command sets, i.e. the text before the first space.</summary>
    private static string CvarOf(string command) => command.Split(' ', 2)[0];

    private static string ValueOf(IReadOnlyList<string> commands, string cvar)
        => Assert.Single(commands, c => CvarOf(c) == cvar).Split(' ', 2)[1];

    [Fact]
    public void EachCvarIsSetExactlyOnce()
    {
        var cvars = CommandsFor().Select(CvarOf).ToList();
        Assert.Equal(cvars.Count, cvars.Distinct().Count());
    }

    [Fact]
    public void TeamsStayTeams_ByDefault()
    {
        var commands = CommandsFor();
        Assert.Equal("0", ValueOf(commands, "mp_teammates_are_enemies"));
        Assert.Equal("0", ValueOf(commands, "mp_solid_teammates"));
        Assert.Equal("0", ValueOf(commands, "mp_friendlyfire"));
    }

    [Fact]
    public void FreeForAll_MakesTeammatesSolidEnemies()
    {
        var commands = CommandsFor(c => c.Gameplay.FreeForAll = true);
        Assert.Equal("1", ValueOf(commands, "mp_teammates_are_enemies"));
        Assert.Equal("1", ValueOf(commands, "mp_solid_teammates"));
    }

    [Fact]
    public void ServerSection_IsApplied()
    {
        var commands = CommandsFor(c =>
        {
            c.Server.FreezeTimeSeconds = 3;
            c.Server.RoundTimeMinutes = 45;
            c.Server.RandomSpawns = false;
            c.Server.DeathCamSeconds = 2f;
        });

        Assert.Equal("3", ValueOf(commands, "mp_freezetime"));
        Assert.Equal("45", ValueOf(commands, "mp_roundtime"));
        Assert.Equal("45", ValueOf(commands, "mp_roundtime_defuse"));
        Assert.Equal("45", ValueOf(commands, "mp_roundtime_hostage"));
        Assert.Equal("0", ValueOf(commands, "mp_randomspawn"));
        Assert.Equal("2", ValueOf(commands, "spec_freeze_time"));
    }

    [Fact]
    public void Numbers_UseInvariantCulture_OnAGermanServer()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var commands = CommandsFor(c => c.Server.DeathCamSeconds = 0.5f);
            Assert.Equal("0.5", ValueOf(commands, "spec_freeze_time"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("cash_player_killed_teammate_factor")]
    [InlineData("mp_weapons_glow_on_ground")]
    [InlineData("spec_freeze_panel_extended_time")]
    [InlineData("mp_molotovusedelay")]
    public void CvarsCs2DoesNotHave_AreNotSet(string cvar)
        => Assert.DoesNotContain(CommandsFor(), c => CvarOf(c) == cvar);

    [Fact]
    public void BotBalancerOwnsTheBotCount()
    {
        var commands = CommandsFor();
        Assert.Equal("0", ValueOf(commands, "bot_quota"));
        Assert.Equal("normal", ValueOf(commands, "bot_quota_mode"));
    }
}
