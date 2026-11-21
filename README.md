# GunGame Lite

[![Build](https://github.com/creazy231/cs2-css-gungame-lite/actions/workflows/build.yml/badge.svg)](https://github.com/creazy231/cs2-css-gungame-lite/actions/workflows/build.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.371-orange.svg)](https://github.com/roflmuffin/CounterStrikeSharp)

A lightweight **GunGame** game mode for **Counter-Strike 2** dedicated servers, written in C# with [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).

Every kill upgrades your weapon. Work your way through the whole arsenal — the first player to score a kill with the final **knife** wins the round. Knife kills and suicides knock you back a level. Late joiners and stragglers are auto-balanced so nobody is stuck at the back.

## Features

- 🔫 **Classic GunGame progression** — 31-weapon ladder from AWP down to the knife, fully configurable.
- ⚔️ **Knife steals a level** — knife an enemy and they drop a weapon; suicides demote too.
- ⚖️ **Catch-up balancing** — late joiners start near the pack and stragglers leap forward on a kill.
- 🛡️ **Spawn protection** — brief, configurable invulnerability on respawn (drops when you shoot).
- 🤖 **Bot filling** — keeps each team topped up to a configurable quota.
- 🏆 **Live HUD & leader callouts** — "about to win" warnings and lead-change announcements.
- 🎯 **Free-for-all** (optional) — everyone against everyone, teammates included.
- ♾️ **Infinite ammo** (optional), configurable grenade re-give, optional warmup.
- 🧪 **Unit-tested core** — all game logic runs and is verified with no CS2 server.

## Requirements

- A Counter-Strike 2 dedicated server with [Metamod:Source](https://www.sourcemm.net/) and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) **v1.0.371+** installed.
- The server must run in **Casual** mode: `+game_type 0 +game_mode 0`. See [Game mode](#game-mode).

## Tested with

v0.3.0 was booted and played (bots, weapon ladder, knife win, warmup, free-for-all, hot reload, map change) on:

| Component | Version |
|---|---|
| Counter-Strike 2 dedicated server (Linux) | 1.42.0.0 (build 2000948, Nov 20 2026) |
| CounterStrikeSharp | v1.0.380 (with runtime) |
| Metamod:Source | 2.0.0-git1491 |

CS2 updates regularly break CounterStrikeSharp itself. If your server also crashes with the `plugins` folder empty, the fix has to come from a CounterStrikeSharp update, not from this plugin.

## Installation

1. Download `GunGameLite.zip` from the [latest release](https://github.com/creazy231/cs2-css-gungame-lite/releases).
2. Extract it into your server's CounterStrikeSharp plugins folder, `game/csgo/addons/counterstrikesharp/plugins/`, so you end up with:
   ```
   game/csgo/addons/counterstrikesharp/plugins/GunGameLite/GunGameLite.dll
   ```
   Zips before v0.3.0 had one extra `GunGameLite/` level inside; if you installed one of those, make sure the DLL sits directly in `plugins/GunGameLite/`.
3. Restart the server (or `css_plugins load GunGameLite`). A config file is generated at:
   ```
   game/csgo/addons/counterstrikesharp/configs/plugins/GunGameLite/GunGameLite.json
   ```

## Game mode

GunGame Lite requires **Casual** — add `+game_type 0 +game_mode 0` to your server's start command
line. Casual is the only stock CS2 mode with neither an engine-side weapon ladder nor engine-side
respawn/loadout logic to fight. The plugin turns it into continuous respawn itself via
`mp_respawn_on_death_t/ct 1`.

| Mode | `game_type` | `game_mode` | Works? |
|---|---|---|---|
| **Casual** | **0** | **0** | ✅ Required |
| Competitive | 0 | 1 | ❌ Half-time team swap, friendly fire, no respawn support |
| Wingman | 0 | 2 | ❌ 2v2 on a small map subset |
| Arms Race | 1 | 0 | ❌ CS2 runs its *own* gun-game ladder and overwrites the plugin's weapons |
| Deathmatch | 1 | 2 | ❌ CS2's own respawn and bonus-weapon logic conflicts |
| Custom | 3 | 0 | ⚠️ Works, but untested and needs a `gamemodes_server.txt` entry |

`game_type` and `game_mode` only take effect on a **map change or server restart**, so the plugin
cannot correct them — it warns instead. On every map it logs the verdict to the server console and
messages any admin (`@css/generic` or `@css/root`) in chat. Regular players never see the warning.
Run `css_ggl_check` to see the current verdict on demand, or set `Server.GameModeCheck` to `false`
to silence it.

### Where to put your own cvars

On every map load CS2 runs `server.cfg`, then `gamemode_casual.cfg`, then
`gamemode_casual_server.cfg`, in that order. The mode config overwrites anything in `server.cfg` that
it also sets, so put your own cvars (`mp_timelimit`, `mp_warmuptime`, …) in
`game/csgo/cfg/gamemode_casual_server.cfg`. Create the file if it doesn't exist.

The cvars GunGame Lite enforces (`css_ggl_check` lists the tunable ones) are re-applied at every round
start, after all three files, so change those through the plugin's `Server` config section instead.

## Free-for-all

Set `Gameplay.FreeForAll` to `true` and everyone fights everyone. The plugin sets
`mp_teammates_are_enemies 1` and `mp_solid_teammates 1`, the same pair CS2's own free-for-all
deathmatch uses, and a kill on a teammate advances you like any other kill. Teams still exist for
spawns, player models and bot balancing. Friendly fire stays off; teammates take damage anyway,
because the game now treats them as enemies.

## Commands

All commands require the `@css/root` permission.

| Command | Description |
|---|---|
| `css_ggl_restart` | Restart the GunGame (`mp_restartgame`). |
| `css_ggl_bots` | Toggle bot filling on/off. |
| `css_ggl_reload` | Hot-reload the plugin. |
| `css_ggl_check` | Report the current game mode and the cvars the plugin enforces. |

## Configuration

`GunGameLite.json` (generated on first load):

| Key | Default | Description |
|---|---|---|
| `Tag` | `{DEFAULT}[{PURPLE}GGL{DEFAULT}]{DEFAULT} ` | Chat prefix (supports `{COLOR}` tokens). |
| `Colors.Highlight` | `{PURPLE}` | Highlight color token. |
| `Bots.Enabled` | `true` | Fill teams with bots. |
| `Bots.QuotaPerTeam` | `2` | Target players (humans + bots) per team. |
| `Bots.Difficulty` | `3` | Bot difficulty `0`–`3`. |
| `Gameplay.Warmup.Enabled` | `false` | Enable a no-progression warmup. |
| `Gameplay.Warmup.Seconds` | `30` | Warmup length. |
| `Gameplay.SpawnProtection.Enabled` | `true` | Invulnerability on spawn. |
| `Gameplay.SpawnProtection.Seconds` | `2.0` | Protection duration. |
| `Gameplay.SpawnProtection.DropOnAttack` | `true` | End protection when the player shoots. |
| `Gameplay.InfiniteAmmo` | `true` | Refill the clip on reload. |
| `Gameplay.InfiniteReserve` | `true` | Keep reserve ammo topped up. |
| `Gameplay.GrenadeRegiveDelaySeconds` | `3.0` | Delay before re-giving a thrown grenade level. |
| `Gameplay.CatchUp.Enabled` | `true` | Rubber-band balancing for joiners/stragglers. |
| `Gameplay.FreeForAll` | `false` | Everyone fights everyone; see [Free-for-all](#free-for-all). |
| `Gameplay.TeamKillsCount` | `false` | If `true`, team kills advance the attacker. Implied by `FreeForAll`. |
| `Gameplay.KnifeStealsLevel` | `true` | Knife kills demote the victim. |
| `Gameplay.DemoteOnSuicide` | `true` | Suicides/world deaths demote the victim. |
| `Hud.ShowProgress` | `true` | Show the progress HUD. |
| `Server.GameModeCheck` | `true` | Warn admins when `game_type`/`game_mode` is wrong. |
| `Server.RandomSpawns` | `true` | `mp_randomspawn` — spread spawns out deathmatch-style. |
| `Server.FreezeTimeSeconds` | `0` | `mp_freezetime`. |
| `Server.RoundTimeMinutes` | `60` | `mp_roundtime` and its defuse/hostage variants. |
| `Server.DeathCamSeconds` | `0.0` | `spec_freeze_time` — death cam length. |
| `WeaponOrder` | 31-weapon ladder | Progression order; **must end with `knife`**. |

## Troubleshooting

- **The server dies right after `GunGameLite vX loaded` (exit code 139).** Fixed in v0.3.0, so
  update. Older versions sent a chat message while the server was still booting, which crashes CS2.
- **A setting from this README is missing from my config file.** CounterStrikeSharp never rewrites an
  existing config, so keys added later (like `Gameplay.FreeForAll` in v0.3.0) silently use their
  default until you add them. Add the key by hand, or delete the file and restart to get a fresh one
  with every key listed.

## Building from source

Requires the **.NET 10 SDK**.

```bash
dotnet restore
dotnet build -c Release      # plugin DLL in GunGameLite/bin/Release/net10.0/
dotnet test  -c Release      # run the unit tests (no CS2 server needed)
```

## Contributing

Issues and PRs welcome. Game logic lives in the CSS-free `GunGameLite/Engine/` and is covered by xUnit tests in `GunGameLite.Tests/` — please add tests for logic changes.

## Credits & License

Created and maintained by **creazy231**. Licensed under **[GPL-3.0](LICENSE)**.
