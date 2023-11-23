using CounterStrikeSharp.API.Core;

namespace GunGameLite;

public static class PrintHelper
{
    public static void PlayerCenterHtml(CCSPlayerController player, string message, int duration = 0)
    {
        if (duration > 0)
        {
            RepeatFor(duration, .05f, () => { PlayerCenterHtml(player, message); });
        }
        else
        {
            player.PrintToCenterHtml(message);
        }
    }

    private static void RepeatFor(float duration, float delay, Action? action)
    {
        if (duration <= 0)
            return;

        action?.Invoke();

        GunGameLite.Instance?.AddTimer(delay, () =>
        {
            if (action != null) RepeatFor(duration - delay, delay, action);
        });
    }
}