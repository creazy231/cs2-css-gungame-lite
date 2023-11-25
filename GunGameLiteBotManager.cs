using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GunGameLite.Extensions;

namespace GunGameLite;

public class BotManager
{
    public static void SpawnBots()
    {
        if (!GunGameLite.Instance!.Config.Bots.Enabled) return;
        Server.ExecuteCommand($"bot_quota 4");
        
        var totalPlayersOnTSide = 0;
        var totalPlayersOnCtSide = 0;
        var botsOnTSide = 0;
        var botsOnCtSide = 0;

        var players = Utilities.GetPlayers();

        foreach (var player in players.Where(player => player is { IsValid: true }))
        {
            if (!player.IsOnATeam()) continue;
            
            if (player.IsTerrorist())
            {
                totalPlayersOnTSide++;
                if (player.IsBot) botsOnTSide++;
                LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> T: {player.PlayerName} is a T");
            }

            if (player.IsCounterTerrorist())
            {
                totalPlayersOnCtSide++;
                if (player.IsBot) botsOnCtSide++;
                LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> CT: {player.PlayerName} is a CT");
            }
        }
        
        LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Spawning bots. T: {totalPlayersOnTSide} CT: {totalPlayersOnCtSide}");
        
        while (totalPlayersOnTSide < 2)
        {
            Server.ExecuteCommand("bot_add t");
            LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Spawning bot on T side since there are {totalPlayersOnTSide} players on T side");
            totalPlayersOnTSide++;
            botsOnTSide++;
        }
        
        while (totalPlayersOnCtSide < 2)
        {
            Server.ExecuteCommand("bot_add ct");
            LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Spawning bot on CT side since there are {totalPlayersOnCtSide} players on CT side");
            totalPlayersOnCtSide++;
            botsOnCtSide++;
        }
        
        // kick bots if there are too many
        while (botsOnTSide > 0 && totalPlayersOnTSide > 2)
        {
            var bot = players.FirstOrDefault(player => player.IsBot && player.IsTerrorist());
            if (bot == null) break;
            
            Server.ExecuteCommand("bot_kick t");
            LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Kicking bot on T side");
            botsOnTSide--;
            totalPlayersOnTSide--;
        }
        
        while (botsOnCtSide > 0 && totalPlayersOnCtSide > 2)
        {
            var bot = players.FirstOrDefault(player => player.IsBot && player.IsCounterTerrorist());
            if (bot == null) break;
            
            Server.ExecuteCommand("bot_kick ct");
            LogHelper.LogToConsole(ConsoleColor.Green, $"[GunGame Lite Plugin] -> Kicking bot on CT side");
            botsOnCtSide--;
            totalPlayersOnCtSide--;
        }
    }
}