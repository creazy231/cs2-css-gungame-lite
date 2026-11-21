using CounterStrikeSharp.API;
using GunGameLite.Config;

namespace GunGameLite.Helpers;

/// <summary>
/// The cvars that enforce GunGame rules. Applied on load and every round start, because the game
/// mode's own cfg is re-executed on map load and would otherwise win.
///
/// Deliberately not set: mp_timelimit (servers drive map rotation with it), sv_infinite_ammo
/// (sv_cheats-gated, so Gameplay.InfiniteAmmo does it by hand instead), mp_warmuptime (the admin's
/// call; the plugin has its own warmup).
///
/// Every cvar here exists in CS2 1.42.0.0. The CS:GO-era cash_player_killed_teammate_factor,
/// mp_weapons_glow_on_ground, spec_freeze_panel_extended_time and mp_molotovusedelay were dropped:
/// CS2 only answered them with "Unknown command" on every round start.
/// </summary>
public static class ServerRuleset
{
    public static void Apply(ServerConfig config)
    {
        // Bots: BotBalancer owns the bot count. bot_quota_mode fill would have the engine top teams
        // up on its own and fight the plugin's bot_add/bot_kick.
        Server.ExecuteCommand("bot_quota 0");
        Server.ExecuteCommand("bot_quota_mode normal");
        Server.ExecuteCommand("bot_chatter off");
        Server.ExecuteCommand("bot_join_after_player 0");

        // Money: GunGame has no economy at all.
        Server.ExecuteCommand("mp_afterroundmoney 0");
        Server.ExecuteCommand("mp_maxmoney 0");
        Server.ExecuteCommand("mp_playercashawards 0");
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_teamcashawards 0");
        Server.ExecuteCommand("cash_player_killed_enemy_default 0");
        Server.ExecuteCommand("cash_player_killed_enemy_factor 0");
        Server.ExecuteCommand("cash_player_killed_teammate 0");

        // Score: player.Score carries the GunGame kill count, so nothing else may write to it.
        Server.ExecuteCommand("contributionscore_assist 0");
        Server.ExecuteCommand("contributionscore_kill 0");
        Server.ExecuteCommand("contributionscore_objective_kill 0");
        Server.ExecuteCommand("contributionscore_suicide 0");

        // Buy menu: money is already zero, so this only stops the menu opening and wasting a keypress.
        Server.ExecuteCommand("mp_buy_allow_grenades 0");
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buy_during_immunity 0");
        Server.ExecuteCommand("mp_buytime 0");

        // Spawn loadout: the ladder is the only weapon source. Without this the engine hands out a
        // free pistol on spawn that GiveWeaponForIndex strips a tick later, which players see as a flicker.
        Server.ExecuteCommand("mp_ct_default_melee weapon_knife");
        Server.ExecuteCommand("mp_ct_default_primary \"\"");
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_melee weapon_knife");
        Server.ExecuteCommand("mp_t_default_primary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_free_armor 2");

        // Drops and pickups: no weapon may enter play except through the ladder.
        Server.ExecuteCommand("mp_death_drop_defuser 0");
        Server.ExecuteCommand("mp_death_drop_grenade 0");
        Server.ExecuteCommand("mp_death_drop_gun 0");
        Server.ExecuteCommand("mp_death_drop_taser 0");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 0");

        // Objectives: no bomb, no defusers. A round ends when someone knifes their way to the top.
        Server.ExecuteCommand("mp_defuser_allocation 0");
        Server.ExecuteCommand("mp_give_player_c4 0");

        // Round flow: one long round with instant respawns, no match structure.
        Server.ExecuteCommand($"mp_freezetime {config.FreezeTimeSeconds}");
        Server.ExecuteCommand("mp_halftime 0");
        Server.ExecuteCommand("mp_halftime_pausetimer 0");
        Server.ExecuteCommand("mp_match_can_clinch 0");
        Server.ExecuteCommand("mp_maxrounds 0");
        Server.ExecuteCommand("mp_respawn_immunitytime 0");
        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");
        Server.ExecuteCommand($"mp_roundtime {config.RoundTimeMinutes}");
        Server.ExecuteCommand($"mp_roundtime_defuse {config.RoundTimeMinutes}");
        Server.ExecuteCommand($"mp_roundtime_hostage {config.RoundTimeMinutes}");
        Server.ExecuteCommand("mp_warmup_pausetimer 0");
        Server.ExecuteCommand("mp_win_panel_display_time 1");

        // Teams: friendly fire would make KnifeStealsLevel farmable on a willing teammate.
        Server.ExecuteCommand("mp_autokick 0");
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("mp_friendlyfire 0");
        Server.ExecuteCommand("mp_limitteams 0");
        Server.ExecuteCommand("mp_solid_teammates 0");

        // Spawns: random spawns spread players out the way a respawn mode needs.
        Server.ExecuteCommand($"mp_randomspawn {(config.RandomSpawns ? 1 : 0)}");
        Server.ExecuteCommand("mp_randomspawn_los 1");

        // Death cam: a long freeze cam fights instant respawn. Invariant culture is required —
        // a de-DE server would otherwise emit "spec_freeze_time 0,5" and the cvar parser would reject it.
        Server.ExecuteCommand("mp_forcecamera 0");
        Server.ExecuteCommand(FormattableString.Invariant($"spec_freeze_time {config.DeathCamSeconds}"));

        // Misc.
        Server.ExecuteCommand("sv_ignoregrenaderadio 1");
    }
}
