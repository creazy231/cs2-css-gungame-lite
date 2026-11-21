using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace GunGameLite.Helpers;

/// <summary>Chat output with {TOKEN} color-tag replacement. Console logging uses BasePlugin.Logger instead.</summary>
public static class ChatHelper
{
    public static void ToChat(CCSPlayerController? player, string message)
    {
        if (player is not { IsValid: true, IsBot: false, IsHLTV: false }) return;
        player.PrintToChat(ReplaceTags(message + "{DEFAULT}"));
    }

    /// <summary>
    /// Chats to every connected human, one player at a time. Deliberately not Server.PrintToChatAll: that
    /// calls the game's UTIL_ClientPrintAll directly, which segfaults the server when it runs before the
    /// game server is up, for example from Load() on a cold boot (issue #1). PrintToChat builds the message
    /// inside CounterStrikeSharp and only ever targets a connected player, so with nobody there it does nothing.
    /// </summary>
    public static void ToChatAll(string message)
    {
        var text = ReplaceTags(message + "{DEFAULT}");
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot || player.IsHLTV) continue;
            player.PrintToChat(text);
        }
    }

    public static string ReplaceTags(this string text)
    {
        if (text.StartsWith('{')) text = $" {text}";
        text = text.Replace("{DEFAULT}", ChatColors.Default.ToString());
        text = text.Replace("{WHITE}", ChatColors.White.ToString());
        text = text.Replace("{DARKRED}", ChatColors.DarkRed.ToString());
        text = text.Replace("{GREEN}", ChatColors.Green.ToString());
        text = text.Replace("{LIGHTYELLOW}", ChatColors.LightYellow.ToString());
        text = text.Replace("{LIGHTBLUE}", ChatColors.LightBlue.ToString());
        text = text.Replace("{OLIVE}", ChatColors.Olive.ToString());
        text = text.Replace("{LIME}", ChatColors.Lime.ToString());
        text = text.Replace("{RED}", ChatColors.Red.ToString());
        text = text.Replace("{PURPLE}", ChatColors.Purple.ToString());
        text = text.Replace("{GREY}", ChatColors.Grey.ToString());
        text = text.Replace("{YELLOW}", ChatColors.Yellow.ToString());
        text = text.Replace("{GOLD}", ChatColors.Gold.ToString());
        text = text.Replace("{SILVER}", ChatColors.Silver.ToString());
        text = text.Replace("{BLUE}", ChatColors.Blue.ToString());
        text = text.Replace("{DARKBLUE}", ChatColors.DarkBlue.ToString());
        text = text.Replace("{BLUEGREY}", ChatColors.BlueGrey.ToString());
        text = text.Replace("{MAGENTA}", ChatColors.Magenta.ToString());
        text = text.Replace("{LIGHTRED}", ChatColors.LightRed.ToString());
        return text;
    }
}
