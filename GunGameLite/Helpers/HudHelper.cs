using CounterStrikeSharp.API.Core;

namespace GunGameLite.Helpers;

/// <summary>Center-screen HTML that persists by re-sending every 0.05s (CS2 center HTML fades fast).</summary>
public static class HudHelper
{
    public static void PlayerCenterHtml(CCSPlayerController player, string message, int duration = 0)
    {
        if (duration > 0)
            RepeatFor(duration, 0.05f, () => PlayerCenterHtml(player, message));
        else
            player.PrintToCenterHtml(message);
    }

    private static void RepeatFor(float duration, float delay, Action action)
    {
        if (duration <= 0) return;
        action.Invoke();
        GunGameLitePlugin.Instance?.AddTimer(delay, () => RepeatFor(duration - delay, delay, action));
    }
}
