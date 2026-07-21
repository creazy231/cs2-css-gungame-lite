# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A **CounterStrikeSharp (CSS) plugin** implementing a "GunGame Lite" mode for **Counter-Strike 2** dedicated servers. It is server-side C# running inside the CS2 server via the CounterStrikeSharp runtime (net10.0, CSS.API 1.0.371). GunGame rule: each kill advances the attacker to the next weapon in `WeaponOrder`; a kill while holding the final knife wins. Knife kills and suicides demote the victim one weapon.

## Layout

- `GunGameLite/` — the plugin project (`GunGameLite.dll`).
  - `GunGameLitePlugin.cs` — thin `BasePlugin` adapter: classifies CS2 events into primitives, calls the engine, applies results to the world. No game logic of its own.
  - `Engine/` — **CSS-free, unit-tested core.** `GunGameEngine` (slot→`PlayerProgress` map, `OnKill`/`OnPlayerJoin`/`Reset`), `WeaponProgression` (index math), `BotBalancer`, `SpawnProtection`, `LeaderTracker`. Import no CS2 runtime calls.
  - `Config/GunGameLiteConfig.cs` — `BasePluginConfig` (Version 2) + `Validate()`.
  - `State/PlayerProgress.cs` — `{ WeaponIndex, Kills }`.
  - `Helpers/` — `ChatHelper` (chat + `{TOKEN}` colors), `HudHelper` (center HTML), `ServerRuleset` (cvar batch).
  - `Extensions/` — `PlayerExtensions`, `HandleExtensions`.
- `GunGameLite.Tests/` — xUnit tests for the engine/helpers/config (no server needed).

## Commands

- Build: `dotnet build -c Release` → `GunGameLite/bin/Release/net10.0/GunGameLite.dll`
- Test: `dotnet test -c Release`
- Release: push a git tag; CI (`.github/workflows/build.yml`) runs test → build → publish.
- Deploy: copy build output to `game/csgo/addons/counterstrikesharp/plugins/GunGameLite/`; hot-reload with `css_plugins reload GunGameLite` or `css_ggl_reload`.

## Conventions

1. **Per-player state is keyed by `player.Slot` (int)**, never by `CCSPlayerController`.
2. **All game logic goes in `Engine/` (CSS-free) and gets a unit test.** The plugin stays a thin adapter.
3. **Deferred execution:** giving weapons / spawning bots is wrapped in `AddTimer(0.1f, …)` because entities aren't ready on the same tick.
4. **Weapon names** in `WeaponOrder` are bare (`"ak47"`); code prepends `weapon_`. Consumables (`hegrenade`/`molotov`) are re-given after `OnWeaponFire`.
5. **Logging** via `Logger` (Microsoft.Extensions.Logging), not `Console.WriteLine`.
6. Before assuming a CSS API exists, check against the referenced version (1.0.371).
