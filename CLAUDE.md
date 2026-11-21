# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A **CounterStrikeSharp (CSS) plugin** implementing a "GunGame Lite" mode for **Counter-Strike 2** dedicated servers. It is server-side C# running inside the CS2 server via the CounterStrikeSharp runtime (net10.0, CSS.API 1.0.371). GunGame rule: each kill advances the attacker to the next weapon in `WeaponOrder`; a kill while holding the final knife wins. Knife kills and suicides demote the victim one weapon.

## Layout

- `GunGameLite/` — the plugin project (`GunGameLite.dll`).
  - `GunGameLitePlugin.cs` — thin `BasePlugin` adapter: classifies CS2 events into primitives, calls the engine, applies results to the world. No game logic of its own.
  - `Engine/` — **CSS-free, unit-tested core.** `GunGameEngine` (slot→`PlayerProgress` map, `OnKill`/`OnPlayerJoin`/`Reset`), `WeaponProgression` (index math), `BotBalancer`, `SpawnProtection`, `LeaderTracker`, `GameModeCheck` (classifies `game_type`/`game_mode`; the plugin can only warn, since neither cvar applies without a map change). Import no CS2 runtime calls.
  - `Config/GunGameLiteConfig.cs` — `BasePluginConfig` (Version 4) + `Validate()`. CSS never rewrites an existing config file and ignores `ConfigVersion`, so a new key must default to the old behaviour.
  - `State/PlayerProgress.cs` — `{ WeaponIndex, Kills }`.
  - `Helpers/` — `ChatHelper` (chat + `{TOKEN}` colors), `HudHelper` (center HTML), `ServerRuleset` (cvar batch: pure `Commands(config)` list, unit-tested, executed by `Apply`).
  - `Extensions/` — `PlayerExtensions`, `HandleExtensions`.
- `GunGameLite.Tests/` — xUnit tests for the engine/helpers/config (no server needed).

## Commands

- Build: `dotnet build -c Release` → `GunGameLite/bin/Release/net10.0/GunGameLite.dll`
- Test: `dotnet test -c Release`
- Release: bump `ModuleVersion` in `GunGameLitePlugin.cs` and push to `main`. CI (`.github/workflows/build.yml`) runs test → build → release, and auto-creates a `vX.Y.Z` GitHub Release with `GunGameLite.zip` (holds `GunGameLite/GunGameLite.dll`; users extract it into `plugins/`) when that version tag doesn't already exist (unchanged versions are a no-op).
- Deploy: copy build output to `game/csgo/addons/counterstrikesharp/plugins/GunGameLite/`; hot-reload with `css_plugins reload GunGameLite` or `css_ggl_reload`.

## Conventions

1. **Per-player state is keyed by `player.Slot` (int)**, never by `CCSPlayerController`.
2. **All game logic goes in `Engine/` (CSS-free) and gets a unit test.** The plugin stays a thin adapter.
3. **Deferred execution:** giving weapons / spawning bots is wrapped in `AddTimer(0.1f, …)` because entities aren't ready on the same tick.
4. **Weapon names** in `WeaponOrder` are bare (`"ak47"`); code prepends `weapon_`. Consumables (`hegrenade`/`molotov`) are re-given after `OnWeaponFire`.
5. **Logging** via `Logger` (Microsoft.Extensions.Logging), not `Console.WriteLine`.
6. Before assuming a CSS API exists, check against the referenced version (1.0.371).
7. **The server must run Casual (`game_type 0` / `game_mode 0`).** Anything else has engine logic that fights the plugin; `GameModeCheck` warns admins about it. Neither cvar can be changed without a map change, so never try to set them from code.
8. **Any `float` interpolated into a `Server.ExecuteCommand` string needs `FormattableString.Invariant`.** A `de-DE` server renders `0.5f` as `"0,5"` and the cvar parser rejects it.
9. **Never call into the game from `Load()` on a cold boot.** CSS loads plugins during server-module init, before the game server exists; v0.2.1's `Server.PrintToChatAll` there segfaulted every boot (issue #1). On a cold boot only register listeners/timers and queue `Server.ExecuteCommand`s; world work waits for `OnMapStart`/`round_start`, and only `hotReload` may touch players.
10. **Chat goes through `ChatHelper`**, which sends `PrintToChat` to each connected human. Never use `Server.PrintToChatAll`: it calls the game's `UTIL_ClientPrintAll` directly and crashes whenever the game server isn't up.
