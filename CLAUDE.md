# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **CounterStrikeSharp (CSS) plugin** implementing a "GunGame Lite" game mode for **Counter-Strike 2** dedicated servers. It is a server-side C# mod, not a standalone app — it runs inside the CS2 server via the CounterStrikeSharp runtime. Every "API" it calls (`Server.ExecuteCommand`, `GiveNamedItem`, event handlers, entity lookups) is CSS talking to the game engine.

GunGame rule: each kill advances the attacker to the next weapon in a fixed progression (`WeaponOrder`); reaching/passing the final weapon (a knife) wins the game. Knife/suicide deaths demote the victim one weapon.

## Commands

- **Build (local dev):** `dotnet build` → DLL lands in `bin/Debug/net7.0/GunGameLite.dll`.
- **Restore:** `dotnet restore`
- **CI build (release):** `dotnet build GunGameLite.csproj -c GunGameLite -o ./GunGameLite/GunGameLite` — `GunGameLite` here is an ad-hoc configuration name (not defined in the csproj; MSBuild treats it as a default-settings config). Don't mistake it for a real Debug/Release variant.
- **No tests, no linter** are configured in this repo.
- Project targets **net7.0**. A newer dotnet SDK can build it, but the net7.0 targeting pack must be present.

### Release flow
Releases are cut by **pushing a git tag** (`.github/workflows/build.yml`, triggered on `push: tags: ["*"]`). CI builds, strips `.deps.json`/`.pdb`, zips to `GunGameLite.zip`, and publishes a GitHub Release named after the tag. Before tagging, manually bump `ModuleVersion` in `GunGameLite.cs` (it is a hardcoded string, e.g. `"0.0.8"`).

### Deploying / testing on a server
Copy the build output into `game/csgo/addons/counterstrikesharp/plugins/GunGameLite/`. To hot-reload after a change without restarting the server, use the in-game console command `css_plugins reload GunGameLite` (or the plugin's own `ggl_rl` admin command).

## Architecture

The whole plugin is a handful of flat top-level `.cs` files (no folders except `Extensions/`), all in namespace `GunGameLite`.

### Core game loop — `GunGameLite.cs`
The `GunGameLite : BasePlugin, IPluginConfig<GunGameLiteConfig>` class is the entry point and holds all game logic. Understand these first:

- **`_playerCurrentWeapon` dictionary** (`CCSPlayerController → weapon name string`) is the single source of truth for each player's progression. It is **cleared on round start and round end**, and rebuilt as players spawn. The player's `.Score` mirrors kill count but progression is driven by this dict, not the scoreboard.
- **Progression:** on kill, `OnPlayerDeath` bumps the attacker's `.Score` and calls `GivePlayerNextWeapon`, which advances the attacker's entry in `_playerCurrentWeapon` to the next `Config.WeaponOrder` element. Passing the last element → `AnnounceWinner` (terminates the round via `CCSGameRulesProxy` / `TerminateRound`).
- **Catch-up balancing:** both `InitializePlayer` (late joiners) and `GivePlayerNextWeapon` clamp a player's weapon index against the *lowest* current index across all players, so newcomers don't start stuck at weapon 0 and leaders don't run away. This is the trickiest logic in the file.
- **Demotion:** deaths where `@event.Weapon` is `"knife"` or `"worldent"` (suicide/world) move the victim back one weapon.
- **Event handlers** are registered two ways — prefer matching whatever the surrounding code does: most via explicit `RegisterEventHandler<T>()` calls in `RegisterListeners()`, plus the `[GameEventHandler]` attribute (see `OnPlayerDisconnect`). Handlers return `HookResult.Continue`.
- **Admin commands** use `[ConsoleCommand]` + `[RequiresPermissions("@css/root")]` + `[CommandHelper]`: `ggl_rr` (restart game), `ggl_bots` (toggle bots), `ggl_rl` (reload plugin), `ggl_test`.

### Two patterns used everywhere — internalize these
1. **`Instance` singleton:** `public static GunGameLite? Instance` is set in `Load` and referenced across files (`BotManager`, `PrintHelper`) to reach the plugin, its `Config`, and `AddTimer`.
2. **Deferred execution via `AddTimer(0.1f, ...)`:** giving weapons, spawning bots, re-arming after grenade throws, etc. are delayed ~0.1s because the target entities/state aren't ready on the same tick the event fires. If a "give item" or spawn action mysteriously does nothing, it almost always needs to be wrapped in a timer.

### Weapon naming convention
`Config.WeaponOrder` stores **bare** names (`"ak47"`, `"awp"`, `"hegrenade"`, `"knife"`). Code prepends `weapon_` when calling `GiveNamedItem($"weapon_{name}")`. Consumable throwables (`"hegrenade"`, `"molotov"`) are re-given after `OnWeaponFire` (via a timer) since firing consumes them.

### Config system — `GunGameLiteConfig.cs`
`GunGameLiteConfig : BasePluginConfig` is auto-serialized to/from JSON by CSS at `addons/counterstrikesharp/configs/plugins/GunGameLite/GunGameLite.json` (generated on first load; `OnConfigParsed` receives it). Holds `Tag`, `Colors`, `Bots` (`Enabled`/`QuotaPerTeam`/`Difficulty`), and the `WeaponOrder` progression array. Changing default progression = edit `WeaponOrder` here.

### Server ruleset — `GunGameLiteServerConfig.cs`
Static `LoadServerConfig()` fires a batch of `mp_*` / `cash_*` / `bot_*` cvars via `Server.ExecuteCommand` to enforce GunGame rules (respawn on death, no money/drops, free armor, spawn immunity, etc.). Called on `Load` and **every round start**, so it re-asserts the ruleset each round. Add rule cvars here, not scattered in the game loop.

### Bots — `GunGameLiteBotManager.cs`
Static `BotManager.SpawnBots()` balances bots to `Config.Bots.QuotaPerTeam` per team, adding/kicking on T/CT sides. Invoked (via timer) after round start, each death, and player disconnect. Team detection uses the `PlayerExtensions` helpers.

### Helpers
- **`LogHelper.cs`** — `LogToConsole` (colored), `LogToChat`/`LogToChatAll`. `ReplaceTags` converts `{PURPLE}`, `{DEFAULT}`, `{GOLD}`… color tokens into CSS `ChatColors`; this is why config/chat strings use `{TOKEN}` syntax. Chat helpers auto-append `{DEFAULT}`.
- **`PrintHelper.cs`** — `PlayerCenterHtml` shows a center-screen HTML message for a duration by **re-sending it every 0.05s** (CS2 center-HTML fades fast, so persistence requires repetition).
- **`Extensions/PlayerExtensions.cs`** — `CCSPlayerController` helpers: `IsAlive`/`IsDying`/`IsDead` (via `LifeState` 0/1/2), `IsTerrorist`/`IsCounterTerrorist` (`TeamNum` **2 = T, 3 = CT**), `IsOnATeam`, `ActiveWeapon`.
- **`Extensions/HandleExtensions.cs`** — `GetValueOrNull<T>` for safe `CHandle<T>` dereferencing.

### `WeaponManagerWeapons.cs` — currently unused
`GunGameLiteWeapons.Weapons` is a static reference table of `WeaponMeta` (def index, display name, `weapon_` name, price). **It is not wired into the game loop** — progression runs entirely off `Config.WeaponOrder` strings. Don't assume this list drives anything; it's a lookup table kept for future use.

## Gotchas
- The plugin depends on `CounterStrikeSharp.API` (NuGet, currently `1.0.123`). API surface shifts between CSS versions — check the referenced version before assuming a method exists.
- Because `_playerCurrentWeapon` is cleared on round boundaries, any logic that reads it must tolerate a missing key mid-transition.
- Weapon-give and bot-spawn side effects are asynchronous (timer-deferred); tests of "did the player get X" must account for the tick delay.
