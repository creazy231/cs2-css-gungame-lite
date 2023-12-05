using CounterStrikeSharp.API.Core;

namespace GunGameLite.Extensions
{
    public static class PlayerExtensions
    {
        public static bool IsAlive(this CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
            {
                // LogHelper.LogToConsole(ConsoleColor.Red, "IsAlive:ER - InvalidPlayer");
                return false;
            }

            var isAlive = player.PlayerPawn.Value != null && player.PlayerPawn.Value.LifeState == 0;
            // LogHelper.LogToConsole(ConsoleColor.Green, $"IsAlive - {player.PlayerName} isAlive? {isAlive}");
            return isAlive;
        }

        public static bool IsDying(this CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
            {
                // LogHelper.LogToConsole(ConsoleColor.Red, "IsDying:ER - InvalidPlayer");
                return false;
            }

            var isDying = player.PlayerPawn.Value != null && player.PlayerPawn.Value.LifeState == 1;
            // LogHelper.LogToConsole(ConsoleColor.Green, $"IsDying - {player.PlayerName} isDying? {isDying}");
            return isDying;
        }

        public static bool IsDead(this CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
            {
                // LogHelper.LogToConsole(ConsoleColor.Red, "IsDead:ER - InvalidPlayer");
                return false;
            }

            var isDead = player.PlayerPawn.Value != null && player.PlayerPawn.Value.LifeState == 2;
            // LogHelper.LogToConsole(ConsoleColor.Green, $"IsDead - {player.PlayerName} isDead? {isDead}");
            return isDead;
        }
        
        public static bool IsTerrorist(this CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
            {
                // LogHelper.LogToConsole(ConsoleColor.Red, "IsTerrorist:ER - InvalidPlayer");
                return false;
            }

            var isTerrorist = player.PlayerPawn.Value != null && player.PlayerPawn.Value.TeamNum == 2;
            // LogHelper.LogToConsole(ConsoleColor.Green, $"IsTerrorist - {player.PlayerName} isTerrorist? {isTerrorist}");
            return isTerrorist;
        }
        
        public static bool IsCounterTerrorist(this CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
            {
                // LogHelper.LogToConsole(ConsoleColor.Red, "IsCounterTerrorist:ER - InvalidPlayer");
                return false;
            }

            var isCounterTerrorist = player.PlayerPawn.Value != null && player.PlayerPawn.Value.TeamNum == 3;
            // LogHelper.LogToConsole(ConsoleColor.Green, $"IsCounterTerrorist - {player.PlayerName} isCounterTerrorist? {isCounterTerrorist}");
            return isCounterTerrorist;
        }

        public static bool IsOnATeam(this CCSPlayerController player)
        {
            return player.IsTerrorist() || player.IsCounterTerrorist();
        }

        public static CBasePlayerWeapon? ActiveWeapon(this CCSPlayerController player)
        {
            if (!player.IsOnATeam()) return null;
            var weaponServices = player.PlayerPawn.Value?.WeaponServices;
            return weaponServices?.ActiveWeapon.GetValueOrNull();
        }
    }
}