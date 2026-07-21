namespace GunGameLite.Engine;

public enum LeaderChange { None, TookLead, StillLeading }

public readonly record struct LeaderMessage(LeaderChange Change, string LeaderName, int Kills, string? PreviousLeaderName);

/// <summary>Pure leader-change detection. The plugin gathers (name, kills) from the scoreboard
/// and formats the chat line; this decides what (if anything) to announce.</summary>
public sealed class LeaderTracker
{
    private string _lastLeader = "";

    /// <summary>Highest scorer with more than zero kills, or null.</summary>
    public static (string name, int kills)? FindTop(IEnumerable<(string name, int kills)> scores)
    {
        (string name, int kills)? top = null;
        foreach (var s in scores)
        {
            if (s.kills <= 0) continue;
            if (top is null || s.kills > top.Value.kills) top = s;
        }
        return top;
    }

    public LeaderMessage Update(string topName, int topKills, string scorerName)
    {
        if (topName == _lastLeader)
        {
            return scorerName == topName
                ? new LeaderMessage(LeaderChange.StillLeading, topName, topKills, null)
                : new LeaderMessage(LeaderChange.None, topName, topKills, null);
        }

        var previous = string.IsNullOrEmpty(_lastLeader) ? null : _lastLeader;
        _lastLeader = topName;
        return new LeaderMessage(LeaderChange.TookLead, topName, topKills, previous);
    }

    public void Reset() => _lastLeader = "";
}
