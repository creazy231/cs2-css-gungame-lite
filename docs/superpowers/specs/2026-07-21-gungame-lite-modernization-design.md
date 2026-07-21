# GunGame Lite — Modernization & Testability Design

- **Date:** 2026-07-21
- **Status:** Approved (pending spec review)
- **Author:** Claude + Tobias Thiele (creazy231)
- **Branch:** `modernize-gungame-net10`

## 1. Context & problem

`GunGameLite` is a CounterStrikeSharp (CSS) plugin implementing a GunGame mode for CS2. It works, but it is ~3 years old and shows it:

- Targets **net7.0** (EOL) on **CounterStrikeSharp.API 1.0.123** (2023). The current ecosystem is **net10.0 / CSS 1.0.371**.
- 9 flat `.cs` files, no solution, **no tests**, an essentially empty README, and dead code (`WeaponManagerWeapons.cs`).
- Game logic is tightly coupled to the CSS runtime, so **nothing is unit-testable** without a live CS2 server.
- Real bugs: welcome message reprints on **every respawn**; knife/suicide demotion can throw **`KeyNotFoundException`**; per-player state is keyed by **`CCSPlayerController`** (an object that can go stale) instead of by player slot.
- Heavy `Console.WriteLine` debug spam instead of the framework logger.

## 2. Goals & non-goals

### Goals
1. Upgrade to **net10.0** + **CounterStrikeSharp.API 1.0.371** and build/run on the current CSS runtime.
2. Restructure into a modern layout mirroring the author's own `cs2-css-flashlight` conventions (root `.sln`, plugin subfolder, extracted testable logic, `.Tests/` xUnit project, test→build→publish CI).
3. Extract game logic into a **CSS-free, unit-testable core** (Approach B: a stateful engine + focused pure helpers). The plugin becomes a thin adapter.
4. Fix the known bugs and add **gameplay/UX polish**: real spawn protection, optional warmup, richer HUD/leaderboard, more config knobs, `css_` command prefix.
5. Ship a comprehensive **xUnit test suite** that runs with no CS2 server (`dotnet test`).
6. Rewrite the **README**, update **CLAUDE.md**, modernize tooling (`.gitignore`, `.editorconfig`, CI), and adopt **GPL-3.0**.

### Non-goals
- Map chooser / LiteMapChooser (README TODO) — deferred; it is really its own plugin.
- Full localization (i18n) — strings stay centralized so it is easy later, but no multi-language resource files now.
- Changing the default weapon progression order (kept identical to today).

## 3. Target stack

| Item | Value |
|---|---|
| Target framework | `net10.0` |
| CSS API | `CounterStrikeSharp.API` `1.0.371` (`[MinimumApiVersion(371)]`) |
| Logging | `Microsoft.Extensions.Logging` via `BasePlugin.Logger` |
| Tests | xUnit `2.9.3`, `Microsoft.NET.Test.Sdk` `17.14.1`, `xunit.runner.visualstudio` `3.1.1`, `coverlet.collector` `6.0.4` |
| License | GPL-3.0 |

Rationale: the CSS API project itself targets net10.0, latest stable is 1.0.371, and the author's flashlight plugin (same conventions) already uses this exact stack. Only the .NET 10 SDK is installed locally, confirming the target.

## 4. Architecture

### 4.1 File layout

```
GunGameLite.sln
GunGameLite/
  GunGameLite.csproj              net10.0, CounterStrikeSharp.API 1.0.371
  GunGameLitePlugin.cs            thin adapter: CS2 event -> engine -> world
  Engine/
    GunGameEngine.cs             CSS-free core; owns Dictionary<int, PlayerProgress>
    WeaponProgression.cs         pure next/prev/catch-up/win index math
    BotBalancer.cs               pure add/kick counts per team
    SpawnProtection.cs           pure protection timing/expiry policy
    LeaderTracker.cs             pure leader-change detection
  Config/GunGameLiteConfig.cs    BasePluginConfig + Validate()
  State/PlayerProgress.cs        plain data { WeaponIndex, Kills }, keyed by slot
  Extensions/
    PlayerExtensions.cs          kept (team/alive helpers)
    HandleExtensions.cs          kept (safe CHandle deref)
  Helpers/
    ChatHelper.cs                (was LogHelper) chat + {TOKEN} color tags
    HudHelper.cs                 (was PrintHelper) center-HTML repeat
    ServerRuleset.cs             (was GunGameLiteServerConfig) cvar batch
GunGameLite.Tests/
  GunGameLite.Tests.csproj       net10.0, references the plugin project
  WeaponProgressionTests.cs
  GunGameEngineTests.cs
  BotBalancerTests.cs
  SpawnProtectionTests.cs
  LeaderTrackerTests.cs
  ConfigValidationTests.cs
.github/workflows/build.yml
.editorconfig
.gitignore  README.md  LICENSE  CLAUDE.md
```

- The DLL/assembly name stays **`GunGameLite`** (csproj `AssemblyName`), so `css_plugins reload GunGameLite` and the `plugins/GunGameLite/` install path are unchanged.
- Entry class renamed `GunGameLite` → **`GunGameLitePlugin`** (matches `FlashlightPlugin`; avoids type/namespace name collision).
- Namespaces: `GunGameLite`, `GunGameLite.Engine`, `GunGameLite.Config`, `GunGameLite.State`, `GunGameLite.Helpers`, `GunGameLite.Extensions`.
- `WeaponManagerWeapons.cs` is **deleted** (dead code, unused).

### 4.2 The CSS-free engine

`GunGameEngine` is the single source of truth for progression. It holds `Dictionary<int, PlayerProgress>` keyed by **`player.Slot`** and works only in integers (weapon indices, kill counts). It imports **no** CSS types, so it constructs and runs in tests with no CS2 runtime.

`PlayerProgress` (plain data):
```
class PlayerProgress { public int WeaponIndex; public int Kills; }
```

`GunGameEngine` public surface (deterministic, exception-safe):
- ctor takes `weaponCount` (n) and a `CatchUpEnabled` flag (and any other pure policy flags).
- `int OnPlayerJoin(int slot)` — register/reset a player; returns their starting index (catch-up adjusted). Idempotent-safe.
- `KillResult OnKill(int attackerSlot, int victimSlot, bool sameTeam, bool weaponIsKnife, bool isSuicide)` — applies the rules below and returns what the plugin must enact.
- `PlayerProgress GetProgress(int slot)` — returns a safe default for unknown slots (never throws).
- `int GetMinIndex(int? excludeSlot = null)` — helper used internally/testably.
- `(string? name, int kills)` is **not** here — leaderboard names live in `LeaderTracker` (see 4.4).
- `void RemovePlayer(int slot)`, `void Reset()`.

`KillResult` (plain data):
```
class KillResult {
  bool AttackerAdvanced;  int AttackerNewIndex;  bool AttackerWon;
  bool VictimDemoted;     int VictimNewIndex;
}
```

### 4.3 Weapon progression math (`WeaponProgression`, pure)

Let `n` = `WeaponOrder.Length`, valid indices `0..n-1`, last index `n-1` = the knife. `minOthers` = minimum `WeaponIndex` over all registered players **excluding** the acting slot (`null` if none).

- **Start index (join):**
  - No others, or catch-up disabled → `0`.
  - Else → `clamp(minOthers - 1, 0, n-1)` (start one gun behind the trailing player).
- **Advance (attacker got a counted kill):**
  - If `cur + 1 >= n` → **win** (they held the final weapon and killed). No further advance.
  - Else `next = cur + 1`; then if catch-up enabled AND others exist AND `next < minOthers - 1` → `next = clamp(minOthers, 0, n-1)` (a straggler leaps up to the pack tail).
- **Demote (victim, knife-steal or suicide):** `newIndex = max(cur - 1, 0)`.
- **Win test:** `cur + 1 >= n`.

All four are pure functions with exhaustive `[Theory]` coverage, including boundaries (index 0, index n-1, solo player, empty pack).

### 4.4 Kill outcome matrix (in `GunGameEngine.OnKill`)

| Situation | Attacker weapon | Victim weapon | Kill-count change |
|---|---|---|---|
| Enemy normal kill (`!sameTeam`, not knife, not suicide) | advance / win | unchanged | attacker +1 |
| Enemy knife kill (`!sameTeam`, `weaponIsKnife`) | advance / win | demote (if `KnifeStealsLevel`) | attacker +1 |
| Suicide / world (`isSuicide`) | none | demote (if `DemoteOnSuicide`) | victim −1 (floor 0) |
| Team kill (`sameTeam`, not suicide) | none (per decision ①) | unchanged | none |

Notes:
- **Decision ①:** team kills never advance or count (config `Gameplay.TeamKillsCount=false` default; when `true`, a team kill behaves like an enemy normal kill).
- Attacker kill count mirrors the scoreboard `Score`; suicide/knife demotion decrements the victim's kills (floor 0), matching the original's `Score--`.
- Unknown slots resolve to a fresh `PlayerProgress` (index 0) rather than throwing — this eliminates the original `KeyNotFoundException`.

### 4.5 Data flow

**Input classification** (done in the plugin from `EventPlayerDeath`, so the engine stays primitive-only):
- `isSuicide` = attacker is `null`/invalid, OR `attackerSlot == victimSlot`, OR weapon is `"worldent"`/`"world"`.
- `weaponIsKnife` = `@event.Weapon == "knife"`.
- `sameTeam` = attacker and victim are on the same team and `attackerSlot != victimSlot`.

CS2 event → `GunGameLitePlugin` handler → classify inputs (slots, `sameTeam`, `weaponIsKnife`, `isSuicide`) → `engine.OnKill(...)` → apply `KillResult` to the world (give weapons via `AddTimer(0.1f, …)`, announce winner / demotion, update `player.Score`), then rebalance bots and update the leader message. The plugin holds **no** progression logic of its own.

## 5. Game rules (behavioral spec)

- **Progression:** one enemy kill = one step up `WeaponOrder`. Reaching the final weapon (knife) prints an "about to win" announcement; a kill while holding the knife wins → `CCSGameRulesProxy.TerminateRound`. (Same as today.)
- **Catch-up (rubber-band):** on by default; late joiners start near the pack tail and stragglers leap forward on a kill (formula in 4.3). Solo play is linear +1.
- **Demotion:** enemy knife kill or suicide/world death demotes the victim one gun (floor 0). Suicide never advances anyone. (Cleaner than today's suicide "wash".)
- **Consumables:** after firing `hegrenade`/`molotov`, re-give the current weapon after `Gameplay.GrenadeRegiveDelaySeconds` (default 3.0, was hardcoded).
- **Ammo:** if `Gameplay.InfiniteAmmo`, top up clip on reload; if `InfiniteReserve`, keep reserve full. Defaults `true` (matches today's reload refill), but no longer a magic `250`.
- **Spawn protection:** on spawn, apply brief invulnerability for `Gameplay.SpawnProtection.Seconds`; if `DropOnAttack`, it ends when the player attacks. Replaces the blunt `mp_respawn_immunitytime`.
- **Warmup (optional, off by default):** when `Gameplay.Warmup.Enabled`, the first `Warmup.Seconds` after load/map start are a no-progression phase — players still spawn and fight with their starting weapon, but kills do not advance/demote and no winner can be declared; the engine begins tracking only when warmup ends.

## 6. Config (`GunGameLiteConfig`, `Version` 1 → 2)

Existing keys retained; a `Gameplay` and `Hud` block added. CSS regenerates the JSON with new fields on first load after upgrade.

```jsonc
{
  "Tag": "...",
  "Colors": { "Highlight": "{PURPLE}" },
  "Bots":   { "Enabled": true, "QuotaPerTeam": 2, "Difficulty": 3 },
  "Gameplay": {
    "Warmup":          { "Enabled": false, "Seconds": 30 },
    "SpawnProtection": { "Enabled": true, "Seconds": 2.0, "DropOnAttack": true },
    "InfiniteAmmo": true,
    "InfiniteReserve": true,
    "GrenadeRegiveDelaySeconds": 3.0,
    "CatchUp": { "Enabled": true },
    "TeamKillsCount": false,
    "KnifeStealsLevel": true,
    "DemoteOnSuicide": true
  },
  "Hud": { "ShowProgress": true },
  "WeaponOrder": [ /* identical default order to today */ ]
}
```

`Validate()` (called from `OnConfigParsed`, like flashlight's `Clamp()`):
- `QuotaPerTeam` clamped to `>= 0`; `Difficulty` clamped to `0..3`.
- All `*Seconds`/delays clamped to `>= 0`.
- `WeaponOrder` must be non-empty and its **last element must be `"knife"`**; otherwise log a warning and fall back to the built-in default order.

## 7. Plugin lifecycle & event handling

Handlers use the `[GameEventHandler]` attribute (flashlight style) where possible; `RegisterListener<>` for tick/listener hooks.

- **OnConfigParsed** — `config.Validate()`; apply bot cvars if bots enabled.
- **Load(hotReload)** — set `Instance`; `ServerRuleset.Apply()`; register handlers; `mp_restartgame 1`; on hot reload, re-init currently-connected players.
- **Unload** — clear state.
- **OnPlayerConnectFull** — greet **once** per connect (greeted-slots set); `engine.OnPlayerJoin`.
- **OnPlayerSpawn** — give current weapon (timer-deferred); apply spawn protection.
- **OnPlayerDeath** — classify → `engine.OnKill` → apply result (advance/win/demote, update `Score`, announcements); rebalance bots; update leader.
- **OnRoundStart** — `ServerRuleset.Apply()`; `engine.Reset()`; remove dropped map weapons; re-init alive players; spawn bots.
- **OnRoundEnd** — `engine.Reset()`.
- **OnWeaponFire** — re-give consumable after configurable delay.
- **OnWeaponReload** — ammo top-up per config.
- **OnPlayerTeam** — re-init player in engine.
- **OnPlayerDisconnect** — `engine.RemovePlayer`; rebalance bots.

Commands (all `@css/root`, `css_` prefixed): `css_ggl_restart`, `css_ggl_bots`, `css_ggl_reload`. Debug-only `ggl_test` removed.

## 8. Helpers

- `ChatHelper` (was `LogHelper`) — `LogToChat`/`LogToChatAll` + `{TOKEN}`→`ChatColors` replacement (kept; config strings depend on it). Console logging moves to `Logger.LogInformation/LogDebug`.
- `HudHelper` (was `PrintHelper`) — center-HTML message that re-sends every 0.05s for a duration (kept; CS2 center-HTML fades fast).
- `ServerRuleset` (was `GunGameLiteServerConfig`) — the `mp_*`/`cash_*`/`bot_*` cvar batch; called on load and every round start. Respawn cvars re-asserted here.

## 9. Testing plan (no CS2 server required)

`GunGameLite.Tests` (xUnit) — one file per unit, ~50+ cases:

- **WeaponProgressionTests** — start index (solo, pack, disabled), advance (linear, straggler snap, win boundary), demote (floor), win test; exhaustive `[Theory]` tables incl. index 0 / n-1.
- **GunGameEngineTests** — replayed sequences: solo run to a win; two-player leader + catch-up; late joiner starts near pack; enemy knife kill demotes victim & advances attacker; suicide demotes only; team kill is a no-op (and counts when `TeamKillsCount=true`); unknown-slot never throws; `Reset()` clears state.
- **BotBalancerTests** — `(humansT, botsT, humansCt, botsCt, quota, enabled)` → exact `{AddT, AddCt, KickT, KickCt}` across many combos, incl. bots disabled and over-quota.
- **SpawnProtectionTests** — remains protected before duration; expires after; ends on attack when `DropOnAttack`.
- **LeaderTrackerTests** — `FindTop`; "took the lead", "took the lead from X", "still leading", ties, empty scoreboard.
- **ConfigValidationTests** — clamps (quota, difficulty, delays), `WeaponOrder` rules (empty / not-ending-in-knife fallback), defaults intact.

`BotBalancer.Compute` and `LeaderTracker` are extracted specifically so this bot/leader logic — previously untestable — becomes pure.

## 10. CI/CD

`.github/workflows/build.yml` mirrors flashlight:
- Triggers: push to `main`, tags `*`, PRs to `main`.
- Jobs: **test** (`dotnet test -c Release`) → **build** (needs test) → **publish** (on tags: build, strip `.deps.json`/`.pdb`, zip, GitHub Release via `ncipollo/release-action`).
- `dotnet 10.0.x`, `actions/checkout@v4`, `actions/setup-dotnet@v4`.

## 11. Docs & tooling

- **README** — full rewrite: intro, feature list, progression explainer, install (CSS prerequisite + `plugins/GunGameLite/` drop path), **config reference table**, commands, build & test from source, contributing, credits (creazy231), GPL-3.0 badge. Styled like the flashlight README.
- **CLAUDE.md** — updated to the new architecture (engine, helpers, folders, net10/1.0.371, test commands).
- **.gitignore** — modernized: `.idea`, `**/bin/`, `**/obj/`, `.DS_Store`. Must **not** ignore `docs/superpowers/` (keep the spec tracked).
- **.editorconfig** — standard C# conventions.
- **LICENSE** — replace MIT/template text with full **GPL-3.0**.
- **`ModuleVersion`** bumped (e.g. `0.0.8` → `0.1.0`).

## 12. Migration / compatibility

- Server admins: drop the new build into `plugins/GunGameLite/` as before; the config JSON gains new `Gameplay`/`Hud` keys automatically (Version 2). Existing custom `WeaponOrder`/`Bots` values are preserved by CSS's config merge.
- Commands renamed to `css_ggl_*`; note in README changelog/credits.

## 13. Risks & mitigations

- **API drift 1.0.123 → 1.0.371:** methods used (`GiveNamedItem`, `RemoveWeapons`, `FindAllEntitiesByDesignerName`, `TerminateRound`, `PlayerPawn`, `Score`, `PawnArmor`) are stable and confirmed against the 1.0.371-based flashlight plugin; verify at build time. Mitigation: build early, fix signatures as compiler flags them.
- **Can't integration-test against a live server:** mitigated by pushing all decision logic into the CSS-free engine/helpers and testing those exhaustively. Event wiring is kept thin and obvious.
- **Behavior change surface (catch-up rewrite, TK rule):** defaults chosen to match today except the deliberate TK anti-boost; catch-up formula pinned by tests to the documented properties.

## 14. Done criteria (verification)

1. `dotnet build -c Release` succeeds on the .NET 10 SDK.
2. `dotnet test -c Release` passes (all ~50+ tests green).
3. Solution loads both projects; `WeaponManagerWeapons.cs` gone; no `Console.WriteLine` in plugin code.
4. README renders with a complete config table; CLAUDE.md matches the new structure; LICENSE is GPL-3.0.
5. CI workflow validated (yaml well-formed; test→build→publish stages present).

## 15. Out of scope (restated)

Map chooser, full i18n, changing the default weapon order, and any feature requiring a live server to validate.
