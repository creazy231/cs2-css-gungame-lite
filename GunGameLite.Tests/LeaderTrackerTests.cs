using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class LeaderTrackerTests
{
    [Fact]
    public void FindTop_EmptyOrAllZero_ReturnsNull()
    {
        Assert.Null(LeaderTracker.FindTop(Array.Empty<(string, int)>()));
        Assert.Null(LeaderTracker.FindTop(new[] { ("A", 0), ("B", 0) }));
    }

    [Fact]
    public void FindTop_PicksHighest()
    {
        var top = LeaderTracker.FindTop(new[] { ("A", 3), ("B", 5), ("C", 1) });
        Assert.Equal(("B", 5), top);
    }

    [Fact]
    public void FindTop_TieKeepsFirstSeen()
    {
        var top = LeaderTracker.FindTop(new[] { ("A", 5), ("B", 5) });
        Assert.Equal(("A", 5), top);
    }

    [Fact]
    public void Update_FirstLeader_TookLeadWithNoPrevious()
    {
        var t = new LeaderTracker();
        var m = t.Update("A", 3, "A");
        Assert.Equal(LeaderChange.TookLead, m.Change);
        Assert.Null(m.PreviousLeaderName);
    }

    [Fact]
    public void Update_SameLeaderScoring_StillLeading()
    {
        var t = new LeaderTracker();
        t.Update("A", 3, "A");
        var m = t.Update("A", 4, "A");
        Assert.Equal(LeaderChange.StillLeading, m.Change);
    }

    [Fact]
    public void Update_SameLeaderButSomeoneElseScored_None()
    {
        var t = new LeaderTracker();
        t.Update("A", 3, "A");
        var m = t.Update("A", 3, "B");
        Assert.Equal(LeaderChange.None, m.Change);
    }

    [Fact]
    public void Update_NewLeader_TookLeadFromPrevious()
    {
        var t = new LeaderTracker();
        t.Update("A", 3, "A");
        var m = t.Update("B", 4, "B");
        Assert.Equal(LeaderChange.TookLead, m.Change);
        Assert.Equal("A", m.PreviousLeaderName);
    }
}
