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
- ♾️ **Infinite ammo** (optional), configurable grenade re-give, optional warmup.
- 🧪 **Unit-tested core** — all game logic runs and is verified with no CS2 server.

## Requirements

- A Counter-Strike 2 dedicated server with [Metamod:Source](https://www.sourcemm.net/) and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) **v1.0.371+** installed.

## Installation

1. Download `GunGameLite.zip` from the [latest release](https://github.com/creazy231/cs2-css-gungame-lite/releases).
2. Extract it into your server's CounterStrikeSharp plugins folder:
   ```
   game/csgo/addons/counterstrikesharp/plugins/GunGameLite/
   ```
3. Restart the server (or `css_plugins load GunGameLite`). A config file is generated at:
   ```
   game/csgo/addons/counterstrikesharp/configs/plugins/GunGameLite/GunGameLite.json
   ```

## Commands

All commands require the `@css/root` permission.

| Command | Description |
|---|---|
| `css_ggl_restart` | Restart the GunGame (`mp_restartgame`). |
| `css_ggl_bots` | Toggle bot filling on/off. |
| `css_ggl_reload` | Hot-reload the plugin. |

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
| `Gameplay.TeamKillsCount` | `false` | If `true`, team kills advance the attacker. |
| `Gameplay.KnifeStealsLevel` | `true` | Knife kills demote the victim. |
| `Gameplay.DemoteOnSuicide` | `true` | Suicides/world deaths demote the victim. |
| `Hud.ShowProgress` | `true` | Show the progress HUD. |
| `WeaponOrder` | 31-weapon ladder | Progression order; **must end with `knife`**. |

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
