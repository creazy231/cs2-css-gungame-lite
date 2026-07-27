using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class GameModeCheckTests
{
    [Fact]
    public void RequiredMode_IsCasual()
    {
        Assert.Equal(0, GameModeCheck.RequiredGameType);
        Assert.Equal(0, GameModeCheck.RequiredGameMode);
    }

    [Fact]
    public void Casual_IsOk()
    {
        var verdict = GameModeCheck.Evaluate(0, 0);
        Assert.Equal(GameModeSeverity.Ok, verdict.Severity);
        Assert.Equal("Casual", verdict.ModeName);
    }

    [Theory]
    [InlineData(1, 0, "Arms Race")]
    [InlineData(1, 2, "Deathmatch")]
    public void ModesWithTheirOwnEngineLogic_AreErrors(int gameType, int gameMode, string expectedName)
    {
        var verdict = GameModeCheck.Evaluate(gameType, gameMode);
        Assert.Equal(GameModeSeverity.Error, verdict.Severity);
        Assert.Equal(expectedName, verdict.ModeName);
    }

    [Fact]
    public void ArmsRace_ReasonNamesTheCompetingWeaponLadder()
    {
        Assert.Contains("weapon ladder", GameModeCheck.Evaluate(1, 0).Reason);
    }

    [Fact]
    public void Deathmatch_ReasonNamesTheCompetingRespawnLogic()
    {
        Assert.Contains("respawn", GameModeCheck.Evaluate(1, 2).Reason);
    }

    [Theory]
    [InlineData(0, 1, "Competitive")]
    [InlineData(0, 2, "Wingman")]
    [InlineData(2, 0, "Skirmish")]
    [InlineData(4, 0, "Guardian")]
    [InlineData(4, 1, "Co-op Strike")]
    public void OtherStockModes_AreWarnings(int gameType, int gameMode, string expectedName)
    {
        var verdict = GameModeCheck.Evaluate(gameType, gameMode);
        Assert.Equal(GameModeSeverity.Warning, verdict.Severity);
        Assert.Equal(expectedName, verdict.ModeName);
    }

    [Fact]
    public void Custom_IsOnlyANotice()
    {
        var verdict = GameModeCheck.Evaluate(3, 0);
        Assert.Equal(GameModeSeverity.Notice, verdict.Severity);
        Assert.Equal("Custom", verdict.ModeName);
    }

    [Theory]
    [InlineData(7, 3)]
    [InlineData(5, 4)]
    [InlineData(3, 2)]
    public void UnrecognisedCombination_WarnsAndReportsRawValues(int gameType, int gameMode)
    {
        var verdict = GameModeCheck.Evaluate(gameType, gameMode);
        Assert.Equal(GameModeSeverity.Warning, verdict.Severity);
        Assert.Contains(gameType.ToString(), verdict.ModeName);
        Assert.Contains(gameMode.ToString(), verdict.ModeName);
    }

    [Fact]
    public void EveryKnownCombination_HasANonEmptyReason()
    {
        var combinations = new[] { (0, 0), (1, 0), (1, 2), (0, 1), (0, 2), (2, 0), (4, 0), (4, 1), (3, 0), (9, 9) };
        foreach (var (gameType, gameMode) in combinations)
            Assert.False(string.IsNullOrWhiteSpace(GameModeCheck.Evaluate(gameType, gameMode).Reason));
    }
}
