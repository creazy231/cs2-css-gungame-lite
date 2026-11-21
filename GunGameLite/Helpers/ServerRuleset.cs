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
    public static void Apply(GunGameLiteConfig config)
    {
        foreach (var command in Commands(config))
            Server.ExecuteCommand(command);
    }

    /// <summary>
    /// The console commands <see cref="Apply"/> runs, in order. Pure, so the tests can check them.
    /// Every number goes through FormattableString.Invariant: a de-DE server would otherwise emit
    /// "spec_freeze_time 0,5", which the cvar parser rejects.
    /// </summary>
    public static IReadOnlyList<string> Commands(GunGameLiteConfig config)
    {
        var server = config.Server;
        var ffa = config.Gameplay.FreeForAll ? 1 : 0;

        return new[]
        {
            // Bots: BotBalancer owns the bot count. bot_quota_mode fill would have the engine top teams
            // up on its own and fight the plugin's bot_add/bot_kick.
            "bot_quota 0",
            "bot_quota_mode normal",
            "bot_chatter off",
            "bot_join_after_player 0",

            // Money: GunGame has no economy at all.
            "mp_afterroundmoney 0",
            "mp_maxmoney 0",
            "mp_playercashawards 0",
            "mp_startmoney 0",
            "mp_teamcashawards 0",
            "cash_player_killed_enemy_default 0",
            "cash_player_killed_enemy_factor 0",
            "cash_player_killed_teammate 0",

            // Score: player.Score carries the GunGame kill count, so nothing else may write to it.
            "contributionscore_assist 0",
            "contributionscore_kill 0",
            "contributionscore_objective_kill 0",
            "contributionscore_suicide 0",

            // Buy menu: money is already zero, so this only stops the menu opening and wasting a keypress.
            "mp_buy_allow_grenades 0",
            "mp_buy_anywhere 0",
            "mp_buy_during_immunity 0",
            "mp_buytime 0",

            // Spawn loadout: the ladder is the only weapon source. Without this the engine hands out a
            // free pistol on spawn that GiveWeaponForIndex strips a tick later, which players see as a flicker.
            "mp_ct_default_melee weapon_knife",
            "mp_ct_default_primary \"\"",
            "mp_ct_default_secondary \"\"",
            "mp_t_default_melee weapon_knife",
            "mp_t_default_primary \"\"",
            "mp_t_default_secondary \"\"",
            "mp_free_armor 2",

            // Drops and pickups: no weapon may enter play except through the ladder.
            "mp_death_drop_defuser 0",
            "mp_death_drop_grenade 0",
            "mp_death_drop_gun 0",
            "mp_death_drop_taser 0",
            "mp_weapons_allow_map_placed 0",

            // Objectives: no bomb, no defusers. A round ends when someone knifes their way to the top.
            "mp_defuser_allocation 0",
            "mp_give_player_c4 0",

            // Round flow: one long round with instant respawns, no match structure.
            FormattableString.Invariant($"mp_freezetime {server.FreezeTimeSeconds}"),
            "mp_halftime 0",
            "mp_halftime_pausetimer 0",
            "mp_match_can_clinch 0",
            "mp_maxrounds 0",
            "mp_respawn_immunitytime 0",
            "mp_respawn_on_death_ct 1",
            "mp_respawn_on_death_t 1",
            FormattableString.Invariant($"mp_roundtime {server.RoundTimeMinutes}"),
            FormattableString.Invariant($"mp_roundtime_defuse {server.RoundTimeMinutes}"),
            FormattableString.Invariant($"mp_roundtime_hostage {server.RoundTimeMinutes}"),
            "mp_warmup_pausetimer 0",
            "mp_win_panel_display_time 1",

            // Teams: friendly fire stays off, or KnifeStealsLevel would be farmable on a willing teammate.
            // Free-for-all makes every player an enemy instead, the way CS2's own FFA deathmatch does
            // (gamemode_deathmatch.cfg: mp_teammates_are_enemies 1, mp_solid_teammates 1).
            "mp_autokick 0",
            "mp_autoteambalance 0",
            "mp_friendlyfire 0",
            "mp_limitteams 0",
            FormattableString.Invariant($"mp_solid_teammates {ffa}"),
            FormattableString.Invariant($"mp_teammates_are_enemies {ffa}"),

            // Spawns: random spawns spread players out the way a respawn mode needs.
            FormattableString.Invariant($"mp_randomspawn {(server.RandomSpawns ? 1 : 0)}"),
            "mp_randomspawn_los 1",

            // Death cam: a long freeze cam fights instant respawn.
            "mp_forcecamera 0",
            FormattableString.Invariant($"spec_freeze_time {server.DeathCamSeconds}"),

            // Misc.
            "sv_ignoregrenaderadio 1",
        };
    }
}
