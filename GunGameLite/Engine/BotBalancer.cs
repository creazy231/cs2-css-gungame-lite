namespace GunGameLite.Engine;

/// <summary>How many bots to add/kick per team to reach the quota.</summary>
public readonly record struct BotBalance(int AddT, int AddCt, int KickT, int KickCt);

/// <summary>Pure bot-balancing math. Quota is a per-team total (humans + bots) target,
/// reached or trimmed using bots only. No CS2 runtime.</summary>
public static class BotBalancer
{
    public static BotBalance Compute(int humansT, int botsT, int humansCt, int botsCt, int quotaPerTeam, bool botsEnabled)
    {
        if (!botsEnabled) return new BotBalance(0, 0, botsT, botsCt);

        var (addT, kickT) = Side(humansT, botsT, quotaPerTeam);
        var (addCt, kickCt) = Side(humansCt, botsCt, quotaPerTeam);
        return new BotBalance(addT, addCt, kickT, kickCt);
    }

    private static (int add, int kick) Side(int humans, int bots, int quota)
    {
        var total = humans + bots;
        if (total < quota) return (quota - total, 0);
        if (total > quota) return (0, Math.Min(bots, total - quota));
        return (0, 0);
    }
}
