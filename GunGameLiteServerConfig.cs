using CounterStrikeSharp.API;

namespace GunGameLite
{
    public static class GunGameLiteServerConfig
    {
        public static void LoadServerConfig()
        {
            Server.ExecuteCommand("bot_chatter off");
            Server.ExecuteCommand("bot_join_after_player 0");
            Server.ExecuteCommand("bot_quota_mode fill");

            Server.ExecuteCommand("cash_player_killed_enemy_default 0");
            Server.ExecuteCommand("cash_player_killed_enemy_factor 0");
            Server.ExecuteCommand("cash_player_killed_teammate 0");
            Server.ExecuteCommand("cash_player_killed_teammate_factor 0");
            
            Server.ExecuteCommand("contributionscore_assist 0");
            Server.ExecuteCommand("contributionscore_kill 0");
            Server.ExecuteCommand("contributionscore_objective_kill 0");
            Server.ExecuteCommand("contributionscore_suicide 0");

            Server.ExecuteCommand("mp_death_drop_defuser 0");
            Server.ExecuteCommand("mp_death_drop_grenade 0");
            Server.ExecuteCommand("mp_death_drop_gun 0");
            Server.ExecuteCommand("mp_free_armor 2");
            Server.ExecuteCommand("mp_maxmoney 0");
            Server.ExecuteCommand("mp_respawn_immunitytime 10");
            Server.ExecuteCommand("mp_respawn_on_death_ct 1");
            Server.ExecuteCommand("mp_respawn_on_death_t 1");
            Server.ExecuteCommand("mp_startmoney 0");
            Server.ExecuteCommand("mp_weapons_allow_map_placed 0");
        }
    }
}