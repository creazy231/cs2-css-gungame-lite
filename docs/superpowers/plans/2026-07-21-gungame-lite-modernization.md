# GunGame Lite Modernization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernize the GunGame Lite CS2/CounterStrikeSharp plugin to net10.0 / CSS 1.0.371 with a CSS-free, unit-tested game engine, gameplay/UX polish, CI, and rewritten docs.

**Architecture:** All game logic lives in a CSS-free `GunGameLite.Engine` (a stateful `GunGameEngine` keyed by player *slot*, plus pure helpers `WeaponProgression`, `BotBalancer`, `SpawnProtection`, `LeaderTracker`) and a validated `GunGameLiteConfig`. The `GunGameLitePlugin` (a `BasePlugin`) is a thin adapter: it classifies CS2 events into primitives, calls the engine, and applies the returned results to the world. Pure code is TDD'd with xUnit and runs with no CS2 server.

**Tech Stack:** C# / net10.0, CounterStrikeSharp.API 1.0.371, xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4.

## Global Constraints

- Target framework: **net10.0** (both projects).
- CSS API: **CounterStrikeSharp.API 1.0.371**; plugin class annotated **`[MinimumApiVersion(371)]`**.
- Assembly/DLL name stays **`GunGameLite`** (so `css_plugins reload GunGameLite` and `plugins/GunGameLite/` are unchanged).
- Per-player state keyed by **`player.Slot` (int)** — never by `CCSPlayerController`.
- Engine and pure helpers (`GunGameLite.Engine.*` except plugin wiring) must import **no** CSS runtime calls (they may reference `CounterStrikeSharp.API.Core` types only where the config needs `BasePluginConfig`).
- Logging via `BasePlugin.Logger` (`Microsoft.Extensions.Logging`) — **no `Console.WriteLine`** in plugin code.
- Default `WeaponOrder` is preserved **exactly** (31 entries, ending in `knife`).
- License: **GPL-3.0**. `ModuleVersion`: **`0.1.0`**.
- Decision ①: team kills do not advance/count by default (`Gameplay.TeamKillsCount=false`).
- Test stack versions: xunit `2.9.3`, `Microsoft.NET.Test.Sdk` `17.14.1`, `xunit.runner.visualstudio` `3.1.1`, `coverlet.collector` `6.0.4`.
- All `dotnet` commands run from the repo root.

---

### Task 1: Restructure to the modern net10 layout (clean slate)

Because the plugin is being fully rewritten, the old flat `.cs` files are deleted here rather than ported against a drifted API. The old behavior is captured in the spec and reproduced in Task 9. `Extensions/` is kept (still valid). A minimal plugin stub keeps the project a valid, compiling CSS plugin.

**Files:**
- Create: `GunGameLite/GunGameLite.csproj`
- Create: `GunGameLite/GunGameLitePlugin.cs` (stub)
- Move: `Extensions/PlayerExtensions.cs` → `GunGameLite/Extensions/PlayerExtensions.cs`
- Move: `Extensions/HandleExtensions.cs` → `GunGameLite/Extensions/HandleExtensions.cs`
- Delete: `GunGameLite.cs`, `GunGameLiteConfig.cs`, `GunGameLiteServerConfig.cs`, `GunGameLiteBotManager.cs`, `LogHelper.cs`, `PrintHelper.cs`, `WeaponManagerWeapons.cs`, and the root `GunGameLite.csproj`
- Create: `GunGameLite.Tests/GunGameLite.Tests.csproj`
- Create: `GunGameLite.sln`

**Interfaces:**
- Produces: solution `GunGameLite.sln` with projects `GunGameLite` (net10, CSS.API 1.0.371, AssemblyName `GunGameLite`) and `GunGameLite.Tests` (xUnit, ProjectReference to `GunGameLite`). Namespace root `GunGameLite`.

- [ ] **Step 1: Move Extensions and delete old sources**

```bash
git rm GunGameLite.cs GunGameLiteConfig.cs GunGameLiteServerConfig.cs GunGameLiteBotManager.cs LogHelper.cs PrintHelper.cs WeaponManagerWeapons.cs GunGameLite.csproj
mkdir -p GunGameLite/Extensions GunGameLite/Engine GunGameLite/Config GunGameLite/State GunGameLite/Helpers GunGameLite.Tests
git mv Extensions/PlayerExtensions.cs GunGameLite/Extensions/PlayerExtensions.cs
git mv Extensions/HandleExtensions.cs GunGameLite/Extensions/HandleExtensions.cs
rmdir Extensions 2>/dev/null || true
```

- [ ] **Step 2: Write `GunGameLite/GunGameLite.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <AssemblyName>GunGameLite</AssemblyName>
        <RootNamespace>GunGameLite</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="CounterStrikeSharp.API" Version="1.0.371" />
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the minimal `GunGameLite/GunGameLitePlugin.cs` stub**

```csharp
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Logging;

namespace GunGameLite;

[MinimumApiVersion(371)]
public class GunGameLitePlugin : BasePlugin
{
    public override string ModuleAuthor => "creazy.eth";
    public override string ModuleName => "GunGameLite";
    public override string ModuleDescription => "A simple GunGame game mode for Counter-Strike 2.";
    public override string ModuleVersion => "0.1.0";

    // Defined now so helpers (Task 8) can reference it; the full body arrives in Task 9.
    public static GunGameLitePlugin? Instance { get; private set; }

    public override void Load(bool hotReload)
    {
        Instance = this;
        Logger.LogInformation("{Name} v{Version} loaded (stub)", ModuleName, ModuleVersion);
    }
}
```

- [ ] **Step 4: Write `GunGameLite.Tests/GunGameLite.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
        <PackageReference Include="xunit" Version="2.9.3" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
        <PackageReference Include="coverlet.collector" Version="6.0.4">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="../GunGameLite/GunGameLite.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 5: Create the solution**

```bash
dotnet new sln -n GunGameLite
dotnet sln add GunGameLite/GunGameLite.csproj GunGameLite.Tests/GunGameLite.Tests.csproj
```

- [ ] **Step 6: Restore and build**

Run: `dotnet build -c Release`
Expected: Build succeeded (both projects), 0 errors. (First run downloads CSS.API 1.0.371 and test packages.)

- [ ] **Step 7: Verify test project runs**

Run: `dotnet test -c Release`
Expected: succeeds; "No test is available" / 0 tests is acceptable at this stage (exit 0).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: restructure to net10/CSS 1.0.371 solution layout"
```

---

### Task 2: WeaponProgression (pure index math)

**Files:**
- Create: `GunGameLite/Engine/WeaponProgression.cs`
- Test: `GunGameLite.Tests/WeaponProgressionTests.cs`

**Interfaces:**
- Produces:
  - `int WeaponProgression.StartIndex(int weaponCount, int? minOthers, bool catchUp)`
  - `bool WeaponProgression.IsWinningKill(int currentIndex, int weaponCount)`
  - `int WeaponProgression.AdvanceIndex(int currentIndex, int weaponCount, int? minOthers, bool catchUp)`
  - `int WeaponProgression.DemoteIndex(int currentIndex)`

- [ ] **Step 1: Write the failing tests**

`GunGameLite.Tests/WeaponProgressionTests.cs`:
```csharp
using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class WeaponProgressionTests
{
    [Theory]
    [InlineData(31, null, true, 0)]   // no other players -> start at 0
    [InlineData(31, 0, true, 0)]      // pack at 0 -> start 0
    [InlineData(31, 10, true, 9)]     // late joiner starts one behind the pack tail
    [InlineData(31, 10, false, 0)]    // catch-up disabled -> always 0
    [InlineData(31, 40, true, 30)]    // clamps to last index
    public void StartIndex_RespectsCatchUpAndBounds(int count, int? minOthers, bool catchUp, int expected)
        => Assert.Equal(expected, WeaponProgression.StartIndex(count, minOthers, catchUp));

    [Theory]
    [InlineData(30, 31, true)]
    [InlineData(29, 31, false)]
    [InlineData(0, 1, true)]
    public void IsWinningKill_TrueOnlyWhenHoldingLastWeapon(int cur, int count, bool expected)
        => Assert.Equal(expected, WeaponProgression.IsWinningKill(cur, count));

    [Theory]
    [InlineData(5, 31, null, true, 6)]   // solo: linear +1
    [InlineData(5, 31, 6, true, 6)]      // ahead of tail-1: linear +1
    [InlineData(3, 31, 10, true, 10)]    // straggler snaps up to pack tail
    [InlineData(3, 31, 10, false, 4)]    // catch-up off: linear +1
    [InlineData(10, 31, 3, true, 11)]    // leader: linear +1
    public void AdvanceIndex_LinearOrSnaps(int cur, int count, int? minOthers, bool catchUp, int expected)
        => Assert.Equal(expected, WeaponProgression.AdvanceIndex(cur, count, minOthers, catchUp));

    [Theory]
    [InlineData(5, 4)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void DemoteIndex_FloorsAtZero(int cur, int expected)
        => Assert.Equal(expected, WeaponProgression.DemoteIndex(cur));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~WeaponProgressionTests"`
Expected: FAIL — `WeaponProgression` does not exist (compile error).

- [ ] **Step 3: Write the implementation**

`GunGameLite/Engine/WeaponProgression.cs`:
```csharp
namespace GunGameLite.Engine;

/// <summary>Pure weapon-index math for GunGame progression. No CS2 runtime.</summary>
public static class WeaponProgression
{
    /// <summary>Starting index for a (possibly late-joining) player.
    /// <paramref name="minOthers"/> is the lowest index among all other players, or null if none.</summary>
    public static int StartIndex(int weaponCount, int? minOthers, bool catchUp)
    {
        if (weaponCount <= 0) return 0;
        if (!catchUp || minOthers is null) return 0;
        return Clamp(minOthers.Value - 1, 0, weaponCount - 1);
    }

    /// <summary>True when a kill made while holding <paramref name="currentIndex"/> wins the game.</summary>
    public static bool IsWinningKill(int currentIndex, int weaponCount)
        => currentIndex + 1 >= weaponCount;

    /// <summary>Next index after a counted kill. Callers must check <see cref="IsWinningKill"/> first.
    /// A straggler (more than one gun behind the pack tail) snaps up to the tail when catch-up is on.</summary>
    public static int AdvanceIndex(int currentIndex, int weaponCount, int? minOthers, bool catchUp)
    {
        var next = currentIndex + 1;
        if (catchUp && minOthers is not null && next < minOthers.Value - 1)
            next = minOthers.Value;
        return Clamp(next, 0, weaponCount - 1);
    }

    /// <summary>Index after a demotion; floors at 0.</summary>
    public static int DemoteIndex(int currentIndex) => Math.Max(currentIndex - 1, 0);

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~WeaponProgressionTests"`
Expected: PASS (all cases).

- [ ] **Step 5: Commit**

```bash
git add GunGameLite/Engine/WeaponProgression.cs GunGameLite.Tests/WeaponProgressionTests.cs
git commit -m "feat: add pure WeaponProgression index math with tests"
```

---

### Task 3: PlayerProgress, KillResult, and the GunGameEngine

**Files:**
- Create: `GunGameLite/State/PlayerProgress.cs`
- Create: `GunGameLite/Engine/KillResult.cs`
- Create: `GunGameLite/Engine/GunGameEngine.cs`
- Test: `GunGameLite.Tests/GunGameEngineTests.cs`

**Interfaces:**
- Consumes: `WeaponProgression` (Task 2).
- Produces:
  - `class PlayerProgress { int WeaponIndex; int Kills; }` (namespace `GunGameLite.State`)
  - `class KillResult { bool AttackerAdvanced; int AttackerNewIndex; bool AttackerWon; bool VictimDemoted; int VictimNewIndex; }`
  - `class GunGameEngine` with ctor `(int weaponCount, bool catchUp=true, bool knifeStealsLevel=true, bool demoteOnSuicide=true, bool teamKillsCount=false)` and members `IReadOnlyDictionary<int,PlayerProgress> Players`, `PlayerProgress GetProgress(int slot)`, `int OnPlayerJoin(int slot)`, `void Seed(int slot,int weaponIndex,int kills=0)`, `KillResult OnKill(int attackerSlot,int victimSlot,bool sameTeam,bool weaponIsKnife,bool isSuicide)`, `void RemovePlayer(int slot)`, `void Reset()`.

- [ ] **Step 1: Write the failing tests**

`GunGameLite.Tests/GunGameEngineTests.cs`:
```csharp
using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class GunGameEngineTests
{
    private static GunGameEngine New(int count = 31) => new(count);

    [Fact]
    public void OnPlayerJoin_FirstPlayerStartsAtZero_AndIsIdempotent()
    {
        var e = New();
        Assert.Equal(0, e.OnPlayerJoin(0));
        e.Seed(0, 5);
        Assert.Equal(5, e.OnPlayerJoin(0)); // already registered -> no reset
    }

    [Fact]
    public void OnPlayerJoin_LateJoinerStartsNearPack()
    {
        var e = New();
        e.Seed(0, 10);
        e.Seed(1, 10);
        Assert.Equal(9, e.OnPlayerJoin(2));
    }

    [Fact]
    public void OnKill_EnemyNormal_AdvancesAttacker_VictimUnchanged()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: false, isSuicide: false);
        Assert.True(r.AttackerAdvanced);
        Assert.Equal(6, r.AttackerNewIndex);
        Assert.False(r.VictimDemoted);
        Assert.Equal(1, e.GetProgress(0).Kills);
        Assert.Equal(5, e.GetProgress(1).WeaponIndex);
    }

    [Fact]
    public void OnKill_EnemyKnife_AdvancesAttacker_DemotesVictim()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: true, isSuicide: false);
        Assert.True(r.AttackerAdvanced);
        Assert.True(r.VictimDemoted);
        Assert.Equal(4, r.VictimNewIndex);
        Assert.Equal(4, e.GetProgress(1).WeaponIndex);
    }

    [Fact]
    public void OnKill_Suicide_DemotesVictim_NoAdvance()
    {
        var e = New();
        e.Seed(0, 5);
        var r = e.OnKill(0, 0, sameTeam: false, weaponIsKnife: false, isSuicide: true);
        Assert.False(r.AttackerAdvanced);
        Assert.True(r.VictimDemoted);
        Assert.Equal(4, e.GetProgress(0).WeaponIndex);
    }

    [Fact]
    public void OnKill_TeamKill_IsNoOpByDefault()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: true, weaponIsKnife: false, isSuicide: false);
        Assert.False(r.AttackerAdvanced);
        Assert.False(r.VictimDemoted);
        Assert.Equal(5, e.GetProgress(0).WeaponIndex);
        Assert.Equal(0, e.GetProgress(0).Kills);
    }

    [Fact]
    public void OnKill_TeamKill_CountsWhenConfigured()
    {
        var e = new GunGameEngine(31, teamKillsCount: true);
        e.Seed(0, 5);
        e.Seed(1, 5);
        var r = e.OnKill(0, 1, sameTeam: true, weaponIsKnife: false, isSuicide: false);
        Assert.True(r.AttackerAdvanced);
        Assert.Equal(6, r.AttackerNewIndex);
    }

    [Fact]
    public void OnKill_WinningKill_SetsWon_NoAdvance()
    {
        var e = New(31);
        e.Seed(0, 30);
        e.Seed(1, 30);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: false, isSuicide: false);
        Assert.True(r.AttackerWon);
        Assert.False(r.AttackerAdvanced);
    }

    [Fact]
    public void OnKill_StragglerSnapsUpToPack()
    {
        var e = New();
        e.Seed(1, 10);
        e.OnPlayerJoin(0); // starts near pack, but seed to force a straggler
        e.Seed(0, 0);
        var r = e.OnKill(0, 1, sameTeam: false, weaponIsKnife: false, isSuicide: false);
        Assert.Equal(10, r.AttackerNewIndex);
    }

    [Fact]
    public void OnKill_UnknownSlots_DoNotThrow()
    {
        var e = New();
        var ex = Record.Exception(() => e.OnKill(7, 8, false, false, false));
        Assert.Null(ex);
        Assert.Equal(1, e.GetProgress(7).Kills);
    }

    [Fact]
    public void Reset_ClearsAllPlayers()
    {
        var e = New();
        e.Seed(0, 5);
        e.Seed(1, 3);
        e.Reset();
        Assert.Empty(e.Players);
    }

    [Fact]
    public void SoloRun_ReachesWin()
    {
        var e = New(5); // 5 weapons: win on the kill made at index 4
        e.OnPlayerJoin(0);
        e.Seed(1, 0); // static victim keeps advance linear
        KillResult r = new();
        for (var i = 0; i < 5; i++)
            r = e.OnKill(0, 1, false, false, false);
        Assert.True(r.AttackerWon);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~GunGameEngineTests"`
Expected: FAIL — types not defined (compile error).

- [ ] **Step 3: Write `GunGameLite/State/PlayerProgress.cs`**

```csharp
namespace GunGameLite.State;

/// <summary>Per-player game state (CSS-free), keyed by player slot in the engine.</summary>
public sealed class PlayerProgress
{
    public int WeaponIndex { get; set; }
    public int Kills { get; set; }
}
```

- [ ] **Step 4: Write `GunGameLite/Engine/KillResult.cs`**

```csharp
namespace GunGameLite.Engine;

/// <summary>What the plugin must enact in the world after a kill.</summary>
public sealed class KillResult
{
    public bool AttackerAdvanced { get; set; }
    public int AttackerNewIndex { get; set; }
    public bool AttackerWon { get; set; }
    public bool VictimDemoted { get; set; }
    public int VictimNewIndex { get; set; }
}
```

- [ ] **Step 5: Write `GunGameLite/Engine/GunGameEngine.cs`**

```csharp
using GunGameLite.State;

namespace GunGameLite.Engine;

/// <summary>The CSS-free core of GunGame Lite. Owns per-slot progression and kill outcomes.
/// Works only in integers, so it runs in unit tests with no CS2 server.</summary>
public sealed class GunGameEngine
{
    private readonly Dictionary<int, PlayerProgress> _players = new();
    private readonly int _weaponCount;
    private readonly bool _catchUp;
    private readonly bool _knifeStealsLevel;
    private readonly bool _demoteOnSuicide;
    private readonly bool _teamKillsCount;

    public GunGameEngine(int weaponCount, bool catchUp = true, bool knifeStealsLevel = true,
                         bool demoteOnSuicide = true, bool teamKillsCount = false)
    {
        _weaponCount = Math.Max(1, weaponCount);
        _catchUp = catchUp;
        _knifeStealsLevel = knifeStealsLevel;
        _demoteOnSuicide = demoteOnSuicide;
        _teamKillsCount = teamKillsCount;
    }

    public IReadOnlyDictionary<int, PlayerProgress> Players => _players;

    /// <summary>Returns the player's stored progress, or a fresh default for unknown slots (never throws).</summary>
    public PlayerProgress GetProgress(int slot)
        => _players.TryGetValue(slot, out var p) ? p : new PlayerProgress();

    /// <summary>Register a player if new (assigning a catch-up start); returns their current weapon index.
    /// Idempotent — existing players are not reset.</summary>
    public int OnPlayerJoin(int slot)
    {
        if (_players.TryGetValue(slot, out var existing)) return existing.WeaponIndex;
        var start = WeaponProgression.StartIndex(_weaponCount, MinIndexExcept(slot), _catchUp);
        _players[slot] = new PlayerProgress { WeaponIndex = start };
        return start;
    }

    /// <summary>Directly set a player's progress (used for round setup and tests).</summary>
    public void Seed(int slot, int weaponIndex, int kills = 0)
        => _players[slot] = new PlayerProgress
        {
            WeaponIndex = Math.Clamp(weaponIndex, 0, _weaponCount - 1),
            Kills = Math.Max(0, kills)
        };

    public KillResult OnKill(int attackerSlot, int victimSlot, bool sameTeam, bool weaponIsKnife, bool isSuicide)
    {
        var result = new KillResult();
        var victim = Ensure(victimSlot);

        if (isSuicide)
        {
            if (_demoteOnSuicide) DemoteVictim(victim, result);
            return result; // suicide never advances anyone
        }

        if (sameTeam && !_teamKillsCount) return result; // team kill: no-op

        var attacker = Ensure(attackerSlot);

        if (weaponIsKnife && _knifeStealsLevel) DemoteVictim(victim, result);

        attacker.Kills += 1;

        if (WeaponProgression.IsWinningKill(attacker.WeaponIndex, _weaponCount))
        {
            result.AttackerWon = true;
            return result;
        }

        attacker.WeaponIndex = WeaponProgression.AdvanceIndex(
            attacker.WeaponIndex, _weaponCount, MinIndexExcept(attackerSlot), _catchUp);
        result.AttackerAdvanced = true;
        result.AttackerNewIndex = attacker.WeaponIndex;
        return result;
    }

    public void RemovePlayer(int slot) => _players.Remove(slot);

    public void Reset() => _players.Clear();

    private static void DemoteVictim(PlayerProgress victim, KillResult result)
    {
        victim.WeaponIndex = WeaponProgression.DemoteIndex(victim.WeaponIndex);
        victim.Kills = Math.Max(0, victim.Kills - 1);
        result.VictimDemoted = true;
        result.VictimNewIndex = victim.WeaponIndex;
    }

    private PlayerProgress Ensure(int slot)
    {
        if (_players.TryGetValue(slot, out var p)) return p;
        p = new PlayerProgress();
        _players[slot] = p;
        return p;
    }

    private int? MinIndexExcept(int slot)
    {
        int? min = null;
        foreach (var (key, value) in _players)
        {
            if (key == slot) continue;
            if (min is null || value.WeaponIndex < min) min = value.WeaponIndex;
        }
        return min;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~GunGameEngineTests"`
Expected: PASS (all cases).

- [ ] **Step 7: Commit**

```bash
git add GunGameLite/State/PlayerProgress.cs GunGameLite/Engine/KillResult.cs GunGameLite/Engine/GunGameEngine.cs GunGameLite.Tests/GunGameEngineTests.cs
git commit -m "feat: add CSS-free GunGameEngine with kill/join/demote/reset and tests"
```

---

### Task 4: BotBalancer (pure bot math)

**Files:**
- Create: `GunGameLite/Engine/BotBalancer.cs`
- Test: `GunGameLite.Tests/BotBalancerTests.cs`

**Interfaces:**
- Produces:
  - `readonly record struct BotBalance(int AddT, int AddCt, int KickT, int KickCt)`
  - `BotBalance BotBalancer.Compute(int humansT, int botsT, int humansCt, int botsCt, int quotaPerTeam, bool botsEnabled)`

- [ ] **Step 1: Write the failing tests**

`GunGameLite.Tests/BotBalancerTests.cs`:
```csharp
using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class BotBalancerTests
{
    [Fact]
    public void FillsEmptyTeamsToQuota()
    {
        var b = BotBalancer.Compute(0, 0, 0, 0, quotaPerTeam: 2, botsEnabled: true);
        Assert.Equal(new BotBalance(2, 2, 0, 0), b);
    }

    [Fact]
    public void OneHumanNeedsOneBot()
    {
        var b = BotBalancer.Compute(1, 0, 0, 0, 2, true);
        Assert.Equal(2, b.AddCt);
        Assert.Equal(1, b.AddT);
    }

    [Fact]
    public void HumansAtQuotaAddNothing()
        => Assert.Equal(new BotBalance(0, 0, 0, 0), BotBalancer.Compute(2, 0, 2, 0, 2, true));

    [Fact]
    public void OverQuotaHumansAreNotKicked()
    {
        var b = BotBalancer.Compute(3, 0, 0, 0, 2, true);
        Assert.Equal(0, b.AddT);
        Assert.Equal(0, b.KickT);
    }

    [Fact]
    public void ExtraBotsAreKicked()
    {
        var b = BotBalancer.Compute(0, 3, 0, 0, 2, true);
        Assert.Equal(1, b.KickT);
        Assert.Equal(0, b.AddT);
    }

    [Fact]
    public void DisabledKicksAllBots()
    {
        var b = BotBalancer.Compute(0, 2, 1, 3, 2, botsEnabled: false);
        Assert.Equal(new BotBalance(0, 0, 2, 3), b);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~BotBalancerTests"`
Expected: FAIL — `BotBalancer` not defined.

- [ ] **Step 3: Write `GunGameLite/Engine/BotBalancer.cs`**

```csharp
namespace GunGameLite.Engine;

/// <summary>How many bots to add/kick per team to reach the quota.</summary>
public readonly record struct BotBalance(int AddT, int AddCt, int KickT, int KickCt);

/// <summary>Pure bot-balancing math. Quota is a per-team total (humans + bots) target,
/// reached or trimmed using bots only. No CS2 runtime.</summary>
public static class BotBalancer
{
    public static BotBalance Compute(int humansT, int botsT, int humansCt, int botsCt, int quotaPerTeam, bool botsEnabled)
    {
        if (!botsEnabled) return new BotBalance(0, 0, botsT, botsCt);

        var (addT, kickT) = Side(humansT, botsT, quotaPerTeam);
        var (addCt, kickCt) = Side(humansCt, botsCt, quotaPerTeam);
        return new BotBalance(addT, addCt, kickT, kickCt);
    }

    private static (int add, int kick) Side(int humans, int bots, int quota)
    {
        var total = humans + bots;
        if (total < quota) return (quota - total, 0);
        if (total > quota) return (0, Math.Min(bots, total - quota));
        return (0, 0);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~BotBalancerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GunGameLite/Engine/BotBalancer.cs GunGameLite.Tests/BotBalancerTests.cs
git commit -m "feat: add pure BotBalancer with tests"
```

---

### Task 5: SpawnProtection (pure policy)

**Files:**
- Create: `GunGameLite/Engine/SpawnProtection.cs`
- Test: `GunGameLite.Tests/SpawnProtectionTests.cs`

**Interfaces:**
- Produces: `bool SpawnProtection.IsActive(bool enabled, double elapsedSeconds, double durationSeconds, bool dropOnAttack, bool hasAttacked)`

- [ ] **Step 1: Write the failing tests**

`GunGameLite.Tests/SpawnProtectionTests.cs`:
```csharp
using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class SpawnProtectionTests
{
    [Fact]
    public void DisabledIsNeverActive()
        => Assert.False(SpawnProtection.IsActive(false, 0, 2, true, false));

    [Fact]
    public void ActiveBeforeDuration()
        => Assert.True(SpawnProtection.IsActive(true, 1.0, 2.0, true, false));

    [Theory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void ExpiresAtOrAfterDuration(double elapsed)
        => Assert.False(SpawnProtection.IsActive(true, elapsed, 2.0, true, false));

    [Fact]
    public void EndsOnAttackWhenDropOnAttack()
        => Assert.False(SpawnProtection.IsActive(true, 0.5, 2.0, dropOnAttack: true, hasAttacked: true));

    [Fact]
    public void AttackIgnoredWhenDropOnAttackOff()
        => Assert.True(SpawnProtection.IsActive(true, 0.5, 2.0, dropOnAttack: false, hasAttacked: true));
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~SpawnProtectionTests"`
Expected: FAIL — `SpawnProtection` not defined.

- [ ] **Step 3: Write `GunGameLite/Engine/SpawnProtection.cs`**

```csharp
namespace GunGameLite.Engine;

/// <summary>Pure spawn-protection timing policy. The plugin owns the actual invulnerability
/// (pawn.TakesDamage) and clock; this only decides whether protection should still hold.</summary>
public static class SpawnProtection
{
    public static bool IsActive(bool enabled, double elapsedSeconds, double durationSeconds, bool dropOnAttack, bool hasAttacked)
    {
        if (!enabled) return false;
        if (dropOnAttack && hasAttacked) return false;
        return elapsedSeconds < durationSeconds;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~SpawnProtectionTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GunGameLite/Engine/SpawnProtection.cs GunGameLite.Tests/SpawnProtectionTests.cs
git commit -m "feat: add pure SpawnProtection policy with tests"
```

---

### Task 6: LeaderTracker (pure leader detection)

**Files:**
- Create: `GunGameLite/Engine/LeaderTracker.cs`
- Test: `GunGameLite.Tests/LeaderTrackerTests.cs`

**Interfaces:**
- Produces:
  - `enum LeaderChange { None, TookLead, StillLeading }`
  - `readonly record struct LeaderMessage(LeaderChange Change, string LeaderName, int Kills, string? PreviousLeaderName)`
  - `static (string name, int kills)? LeaderTracker.FindTop(IEnumerable<(string name, int kills)> scores)`
  - `LeaderMessage LeaderTracker.Update(string topName, int topKills, string scorerName)` (instance)
  - `void LeaderTracker.Reset()`

- [ ] **Step 1: Write the failing tests**

`GunGameLite.Tests/LeaderTrackerTests.cs`:
```csharp
using GunGameLite.Engine;
using Xunit;

namespace GunGameLite.Tests;

public class LeaderTrackerTests
{
    [Fact]
    public void FindTop_EmptyOrAllZero_ReturnsNull()
    {
        Assert.Null(LeaderTracker.FindTop(Array.Empty<(string, int)>()));
        Assert.Null(LeaderTracker.FindTop(new[] { ("A", 0), ("B", 0) }));
    }

    [Fact]
    public void FindTop_PicksHighest()
    {
        var top = LeaderTracker.FindTop(new[] { ("A", 3), ("B", 5), ("C", 1) });
        Assert.Equal(("B", 5), top);
    }

    [Fact]
    public void FindTop_TieKeepsFirstSeen()
    {
        var top = LeaderTracker.FindTop(new[] { ("A", 5), ("B", 5) });
        Assert.Equal(("A", 5), top);
    }

    [Fact]
    public void Update_FirstLeader_TookLeadWithNoPrevious()
    {
        var t = new LeaderTracker();
        var m = t.Update("A", 3, "A");
        Assert.Equal(LeaderChange.TookLead, m.Change);
        Assert.Null(m.PreviousLeaderName);
    }

    [Fact]
    public void Update_SameLeaderScoring_StillLeading()
    {
        var t = new LeaderTracker();
        t.Update("A", 3, "A");
        var m = t.Update("A", 4, "A");
        Assert.Equal(LeaderChange.StillLeading, m.Change);
    }

    [Fact]
    public void Update_SameLeaderButSomeoneElseScored_None()
    {
        var t = new LeaderTracker();
        t.Update("A", 3, "A");
        var m = t.Update("A", 3, "B");
        Assert.Equal(LeaderChange.None, m.Change);
    }

    [Fact]
    public void Update_NewLeader_TookLeadFromPrevious()
    {
        var t = new LeaderTracker();
        t.Update("A", 3, "A");
        var m = t.Update("B", 4, "B");
        Assert.Equal(LeaderChange.TookLead, m.Change);
        Assert.Equal("A", m.PreviousLeaderName);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~LeaderTrackerTests"`
Expected: FAIL — `LeaderTracker` not defined.

- [ ] **Step 3: Write `GunGameLite/Engine/LeaderTracker.cs`**

```csharp
namespace GunGameLite.Engine;

public enum LeaderChange { None, TookLead, StillLeading }

public readonly record struct LeaderMessage(LeaderChange Change, string LeaderName, int Kills, string? PreviousLeaderName);

/// <summary>Pure leader-change detection. The plugin gathers (name, kills) from the scoreboard
/// and formats the chat line; this decides what (if anything) to announce.</summary>
public sealed class LeaderTracker
{
    private string _lastLeader = "";

    /// <summary>Highest scorer with more than zero kills, or null.</summary>
    public static (string name, int kills)? FindTop(IEnumerable<(string name, int kills)> scores)
    {
        (string name, int kills)? top = null;
        foreach (var s in scores)
        {
            if (s.kills <= 0) continue;
            if (top is null || s.kills > top.Value.kills) top = s;
        }
        return top;
    }

    public LeaderMessage Update(string topName, int topKills, string scorerName)
    {
        if (topName == _lastLeader)
        {
            return scorerName == topName
                ? new LeaderMessage(LeaderChange.StillLeading, topName, topKills, null)
                : new LeaderMessage(LeaderChange.None, topName, topKills, null);
        }

        var previous = string.IsNullOrEmpty(_lastLeader) ? null : _lastLeader;
        _lastLeader = topName;
        return new LeaderMessage(LeaderChange.TookLead, topName, topKills, previous);
    }

    public void Reset() => _lastLeader = "";
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~LeaderTrackerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GunGameLite/Engine/LeaderTracker.cs GunGameLite.Tests/LeaderTrackerTests.cs
git commit -m "feat: add pure LeaderTracker with tests"
```

---

### Task 7: GunGameLiteConfig with Validate()

**Files:**
- Create: `GunGameLite/Config/GunGameLiteConfig.cs`
- Test: `GunGameLite.Tests/ConfigValidationTests.cs`

**Interfaces:**
- Produces: `GunGameLite.Config.GunGameLiteConfig : BasePluginConfig` with properties `Tag`, `Colors`, `Bots` (`BotsConfig`), `Gameplay` (`GameplayConfig`), `Hud` (`HudConfig`), `WeaponOrder` (`string[]`), `static string[] DefaultWeaponOrder`, and `void Validate()`. Nested config classes: `Colors`, `BotsConfig{Enabled,QuotaPerTeam,Difficulty}`, `WarmupConfig{Enabled,Seconds}`, `SpawnProtectionConfig{Enabled,Seconds,DropOnAttack}`, `CatchUpConfig{Enabled}`, `GameplayConfig{Warmup,SpawnProtection,InfiniteAmmo,InfiniteReserve,GrenadeRegiveDelaySeconds,CatchUp,TeamKillsCount,KnifeStealsLevel,DemoteOnSuicide}`, `HudConfig{ShowProgress}`.

- [ ] **Step 1: Write the failing tests**

`GunGameLite.Tests/ConfigValidationTests.cs`:
```csharp
using GunGameLite.Config;
using Xunit;

namespace GunGameLite.Tests;

public class ConfigValidationTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var c = new GunGameLiteConfig();
        Assert.Equal(2, c.Version);
        Assert.Equal(2, c.Bots.QuotaPerTeam);
        Assert.True(c.Gameplay.InfiniteAmmo);
        Assert.False(c.Gameplay.TeamKillsCount);
        Assert.Equal("knife", c.WeaponOrder[^1]);
        Assert.Equal(31, c.WeaponOrder.Length);
    }

    [Fact]
    public void Validate_ClampsNumericRanges()
    {
        var c = new GunGameLiteConfig();
        c.Bots.QuotaPerTeam = -5;
        c.Bots.Difficulty = 9;
        c.Gameplay.GrenadeRegiveDelaySeconds = -1f;
        c.Gameplay.SpawnProtection.Seconds = -2f;
        c.Gameplay.Warmup.Seconds = -3;

        c.Validate();

        Assert.Equal(0, c.Bots.QuotaPerTeam);
        Assert.Equal(3, c.Bots.Difficulty);
        Assert.Equal(0f, c.Gameplay.GrenadeRegiveDelaySeconds);
        Assert.Equal(0f, c.Gameplay.SpawnProtection.Seconds);
        Assert.Equal(0, c.Gameplay.Warmup.Seconds);
    }

    [Fact]
    public void Validate_DifficultyBelowRangeClampsToZero()
    {
        var c = new GunGameLiteConfig();
        c.Bots.Difficulty = -1;
        c.Validate();
        Assert.Equal(0, c.Bots.Difficulty);
    }

    [Fact]
    public void Validate_EmptyWeaponOrder_FallsBackToDefault()
    {
        var c = new GunGameLiteConfig { WeaponOrder = Array.Empty<string>() };
        c.Validate();
        Assert.Equal(GunGameLiteConfig.DefaultWeaponOrder, c.WeaponOrder);
    }

    [Fact]
    public void Validate_WeaponOrderNotEndingInKnife_FallsBackToDefault()
    {
        var c = new GunGameLiteConfig { WeaponOrder = new[] { "ak47", "awp" } };
        c.Validate();
        Assert.Equal("knife", c.WeaponOrder[^1]);
    }

    [Fact]
    public void Validate_ValidCustomWeaponOrder_IsPreserved()
    {
        var custom = new[] { "glock", "deagle", "knife" };
        var c = new GunGameLiteConfig { WeaponOrder = custom };
        c.Validate();
        Assert.Equal(custom, c.WeaponOrder);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~ConfigValidationTests"`
Expected: FAIL — `GunGameLite.Config.GunGameLiteConfig` not defined.

- [ ] **Step 3: Write `GunGameLite/Config/GunGameLiteConfig.cs`**

```csharp
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace GunGameLite.Config;

public sealed class Colors
{
    [JsonPropertyName("Highlight")] public string Highlight { get; set; } = "{PURPLE}";
}

public sealed class BotsConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("QuotaPerTeam")] public int QuotaPerTeam { get; set; } = 2;
    [JsonPropertyName("Difficulty")] public int Difficulty { get; set; } = 3;
}

public sealed class WarmupConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = false;
    [JsonPropertyName("Seconds")] public int Seconds { get; set; } = 30;
}

public sealed class SpawnProtectionConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("Seconds")] public float Seconds { get; set; } = 2.0f;
    [JsonPropertyName("DropOnAttack")] public bool DropOnAttack { get; set; } = true;
}

public sealed class CatchUpConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
}

public sealed class GameplayConfig
{
    [JsonPropertyName("Warmup")] public WarmupConfig Warmup { get; set; } = new();
    [JsonPropertyName("SpawnProtection")] public SpawnProtectionConfig SpawnProtection { get; set; } = new();
    [JsonPropertyName("InfiniteAmmo")] public bool InfiniteAmmo { get; set; } = true;
    [JsonPropertyName("InfiniteReserve")] public bool InfiniteReserve { get; set; } = true;
    [JsonPropertyName("GrenadeRegiveDelaySeconds")] public float GrenadeRegiveDelaySeconds { get; set; } = 3.0f;
    [JsonPropertyName("CatchUp")] public CatchUpConfig CatchUp { get; set; } = new();
    [JsonPropertyName("TeamKillsCount")] public bool TeamKillsCount { get; set; } = false;
    [JsonPropertyName("KnifeStealsLevel")] public bool KnifeStealsLevel { get; set; } = true;
    [JsonPropertyName("DemoteOnSuicide")] public bool DemoteOnSuicide { get; set; } = true;
}

public sealed class HudConfig
{
    [JsonPropertyName("ShowProgress")] public bool ShowProgress { get; set; } = true;
}

public sealed class GunGameLiteConfig : BasePluginConfig
{
    public override int Version => 2;

    [JsonPropertyName("Tag")] public string Tag { get; set; } = "{DEFAULT}[{PURPLE}GGL{DEFAULT}]{DEFAULT} ";
    [JsonPropertyName("Colors")] public Colors Colors { get; set; } = new();
    [JsonPropertyName("Bots")] public BotsConfig Bots { get; set; } = new();
    [JsonPropertyName("Gameplay")] public GameplayConfig Gameplay { get; set; } = new();
    [JsonPropertyName("Hud")] public HudConfig Hud { get; set; } = new();
    [JsonPropertyName("WeaponOrder")] public string[] WeaponOrder { get; set; } = (string[])DefaultWeaponOrder.Clone();

    /// <summary>The classic GunGame Lite progression (rifles/snipers/LMGs -> SMGs/shotguns -> scout -> pistols -> HE -> knife).</summary>
    public static readonly string[] DefaultWeaponOrder =
    {
        "awp", "mag7", "g3sg1", "negev", "scar20", "m249",
        "ak47", "m4a1", "sg556", "aug", "galilar", "famas",
        "mac10", "mp9", "bizon", "p90", "sawedoff", "ump45", "xm1014", "mp7", "nova",
        "ssg08",
        "deagle", "elite", "tec9", "hkp2000", "fiveseven", "p250", "glock",
        "hegrenade", "knife",
    };

    /// <summary>Clamp out-of-range values and guarantee a usable weapon order. Call from OnConfigParsed.</summary>
    public void Validate()
    {
        if (Bots.QuotaPerTeam < 0) Bots.QuotaPerTeam = 0;
        Bots.Difficulty = Math.Clamp(Bots.Difficulty, 0, 3);

        if (Gameplay.GrenadeRegiveDelaySeconds < 0f) Gameplay.GrenadeRegiveDelaySeconds = 0f;
        if (Gameplay.SpawnProtection.Seconds < 0f) Gameplay.SpawnProtection.Seconds = 0f;
        if (Gameplay.Warmup.Seconds < 0) Gameplay.Warmup.Seconds = 0;

        if (WeaponOrder is null || WeaponOrder.Length == 0 || WeaponOrder[^1] != "knife")
            WeaponOrder = (string[])DefaultWeaponOrder.Clone();
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~ConfigValidationTests"`
Expected: PASS.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test -c Release`
Expected: PASS — all tests from Tasks 2–7 green.

- [ ] **Step 6: Commit**

```bash
git add GunGameLite/Config/GunGameLiteConfig.cs GunGameLite.Tests/ConfigValidationTests.cs
git commit -m "feat: add validated GunGameLiteConfig v2 with tests"
```

---

### Task 8: Helpers and Extensions

Ports the chat/HUD/ruleset helpers into `GunGameLite.Helpers` and updates the kept extensions' namespace. These use the CSS API and are verified by compilation.

**Files:**
- Create: `GunGameLite/Helpers/ChatHelper.cs`
- Create: `GunGameLite/Helpers/HudHelper.cs`
- Create: `GunGameLite/Helpers/ServerRuleset.cs`
- Modify: `GunGameLite/Extensions/PlayerExtensions.cs` (namespace only — already `GunGameLite.Extensions`, no change needed if it matches; confirm)
- Modify: `GunGameLite/Extensions/HandleExtensions.cs` (confirm namespace `GunGameLite.Extensions`)

**Interfaces:**
- Consumes: `GunGameLitePlugin.Instance` (already declared on the Task 1 stub) — `HudHelper` uses it for `AddTimer`.
- Produces:
  - `ChatHelper.ToChat(CCSPlayerController?, string)`, `ChatHelper.ToChatAll(string)`, `string ChatHelper.ReplaceTags(this string)`
  - `HudHelper.PlayerCenterHtml(CCSPlayerController, string, int duration=0)`
  - `ServerRuleset.Apply()`

- [ ] **Step 1: Write `GunGameLite/Helpers/ChatHelper.cs`**

```csharp
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace GunGameLite.Helpers;

/// <summary>Chat output with {TOKEN} color-tag replacement. Console logging uses BasePlugin.Logger instead.</summary>
public static class ChatHelper
{
    public static void ToChat(CCSPlayerController? player, string message)
        => player?.PrintToChat(ReplaceTags(message + "{DEFAULT}"));

    public static void ToChatAll(string message)
        => Server.PrintToChatAll(ReplaceTags(message + "{DEFAULT}"));

    public static string ReplaceTags(this string text)
    {
        if (text.StartsWith('{')) text = $" {text}";
        text = text.Replace("{DEFAULT}", ChatColors.Default.ToString());
        text = text.Replace("{WHITE}", ChatColors.White.ToString());
        text = text.Replace("{DARKRED}", ChatColors.Darkred.ToString());
        text = text.Replace("{GREEN}", ChatColors.Green.ToString());
        text = text.Replace("{LIGHTYELLOW}", ChatColors.LightYellow.ToString());
        text = text.Replace("{LIGHTBLUE}", ChatColors.LightBlue.ToString());
        text = text.Replace("{OLIVE}", ChatColors.Olive.ToString());
        text = text.Replace("{LIME}", ChatColors.Lime.ToString());
        text = text.Replace("{RED}", ChatColors.Red.ToString());
        text = text.Replace("{PURPLE}", ChatColors.Purple.ToString());
        text = text.Replace("{GREY}", ChatColors.Grey.ToString());
        text = text.Replace("{YELLOW}", ChatColors.Yellow.ToString());
        text = text.Replace("{GOLD}", ChatColors.Gold.ToString());
        text = text.Replace("{SILVER}", ChatColors.Silver.ToString());
        text = text.Replace("{BLUE}", ChatColors.Blue.ToString());
        text = text.Replace("{DARKBLUE}", ChatColors.DarkBlue.ToString());
        text = text.Replace("{BLUEGREY}", ChatColors.BlueGrey.ToString());
        text = text.Replace("{MAGENTA}", ChatColors.Magenta.ToString());
        text = text.Replace("{LIGHTRED}", ChatColors.LightRed.ToString());
        return text;
    }
}
```

- [ ] **Step 2: Write `GunGameLite/Helpers/HudHelper.cs`**

```csharp
using CounterStrikeSharp.API.Core;

namespace GunGameLite.Helpers;

/// <summary>Center-screen HTML that persists by re-sending every 0.05s (CS2 center HTML fades fast).</summary>
public static class HudHelper
{
    public static void PlayerCenterHtml(CCSPlayerController player, string message, int duration = 0)
    {
        if (duration > 0)
            RepeatFor(duration, 0.05f, () => PlayerCenterHtml(player, message));
        else
            player.PrintToCenterHtml(message);
    }

    private static void RepeatFor(float duration, float delay, Action action)
    {
        if (duration <= 0) return;
        action.Invoke();
        GunGameLitePlugin.Instance?.AddTimer(delay, () => RepeatFor(duration - delay, delay, action));
    }
}
```

- [ ] **Step 3: Write `GunGameLite/Helpers/ServerRuleset.cs`**

```csharp
using CounterStrikeSharp.API;

namespace GunGameLite.Helpers;

/// <summary>The mp_*/cash_*/bot_* cvars that enforce GunGame rules. Applied on load and every round start.</summary>
public static class ServerRuleset
{
    public static void Apply()
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
        Server.ExecuteCommand("mp_respawn_immunitytime 0");
        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 0");
    }
}
```

Note: `mp_respawn_immunitytime` is now `0` because spawn protection is handled by the plugin.

- [ ] **Step 4: Confirm the Extensions namespaces**

Open `GunGameLite/Extensions/PlayerExtensions.cs` and `GunGameLite/Extensions/HandleExtensions.cs`; ensure both declare `namespace GunGameLite.Extensions`. They already do — no edit needed. (If either uses the block-scoped `namespace GunGameLite.Extensions { ... }` form, leave as-is; it compiles.)

- [ ] **Step 5: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded. `HudHelper` compiles because the Task 1 stub already declares `GunGameLitePlugin.Instance`.

- [ ] **Step 6: Commit**

```bash
git add GunGameLite/Helpers/
git commit -m "feat: add ChatHelper, HudHelper, ServerRuleset helpers"
```

---

### Task 9: GunGameLitePlugin (the thin adapter)

Replaces the Task 1 stub with the full plugin: config binding, engine wiring, event handlers, spawn protection, warmup, ammo, commands. Not unit-tested (needs a server); verified by compilation and by delegating all decisions to the tested engine.

**Files:**
- Modify: `GunGameLite/GunGameLitePlugin.cs` (replace stub entirely)

**Interfaces:**
- Consumes: `GunGameEngine`, `KillResult` (Task 3); `BotBalancer`/`BotBalance` (Task 4); `LeaderTracker`/`LeaderChange` (Task 6); `GunGameLiteConfig` (Task 7); `ChatHelper`, `HudHelper`, `ServerRuleset` (Task 8); `PlayerExtensions` (`IsTerrorist`, `IsCounterTerrorist`, `IsOnATeam`, `ActiveWeapon`).
- Produces: `public static GunGameLitePlugin? Instance` (consumed by `HudHelper`).

- [ ] **Step 1: Replace `GunGameLite/GunGameLitePlugin.cs` with the full adapter**

```csharp
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using GunGameLite.Config;
using GunGameLite.Engine;
using GunGameLite.Extensions;
using GunGameLite.Helpers;
using Microsoft.Extensions.Logging;

namespace GunGameLite;

[MinimumApiVersion(371)]
public class GunGameLitePlugin : BasePlugin, IPluginConfig<GunGameLiteConfig>
{
    public override string ModuleAuthor => "creazy.eth";
    public override string ModuleName => "GunGameLite";
    public override string ModuleDescription => "A simple GunGame game mode for Counter-Strike 2.";
    public override string ModuleVersion => "0.1.0";

    public static GunGameLitePlugin? Instance { get; private set; }
    public GunGameLiteConfig Config { get; set; } = new();

    private GunGameEngine _engine = new(GunGameLiteConfig.DefaultWeaponOrder.Length);
    private readonly LeaderTracker _leader = new();
    private readonly HashSet<int> _greeted = new();
    private readonly HashSet<int> _protected = new();
    private bool _warmupActive;

    public void OnConfigParsed(GunGameLiteConfig config)
    {
        config.Validate();
        Config = config;
        _engine = new GunGameEngine(
            config.WeaponOrder.Length,
            config.Gameplay.CatchUp.Enabled,
            config.Gameplay.KnifeStealsLevel,
            config.Gameplay.DemoteOnSuicide,
            config.Gameplay.TeamKillsCount);

        if (config.Bots.Enabled)
        {
            Server.ExecuteCommand("mp_autoteambalance 0");
            Server.ExecuteCommand("mp_limitteams 0");
            Server.ExecuteCommand($"bot_difficulty {config.Bots.Difficulty}");
        }
    }

    public override void Load(bool hotReload)
    {
        Instance = this;
        ServerRuleset.Apply();

        RegisterListener<Listeners.OnMapStart>(_ => { if (Config.Gameplay.Warmup.Enabled) StartWarmup(); });

        Server.ExecuteCommand("mp_restartgame 1");
        Logger.LogInformation("{Name} v{Version} loaded", ModuleName, ModuleVersion);
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}GunGame Lite v{ModuleVersion} loaded!");

        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true }))
            {
                _engine.OnPlayerJoin(player.Slot);
                if (!player.IsBot) _greeted.Add(player.Slot);
            }
        }

        if (Config.Gameplay.Warmup.Enabled) StartWarmup();
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("{Name} unloaded", ModuleName);
    }

    // ---- Player lifecycle ----

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid) return HookResult.Continue;

        _engine.OnPlayerJoin(player.Slot);
        if (!player.IsBot && _greeted.Add(player.Slot)) Greet(player);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsHLTV || !player.PawnIsAlive) return HookResult.Continue;

        _engine.OnPlayerJoin(player.Slot);
        GiveWeaponForIndex(player, _engine.GetProgress(player.Slot).WeaponIndex);
        ApplySpawnProtection(player);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid) return HookResult.Continue;
        _engine.OnPlayerJoin(player.Slot); // idempotent: registers late joiners without resetting
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null) return HookResult.Continue;

        _engine.RemovePlayer(player.Slot);
        _greeted.Remove(player.Slot);
        _protected.Remove(player.Slot);
        if (!player.IsBot) AddTimer(0.1f, ApplyBotBalance);
        return HookResult.Continue;
    }

    // ---- Core loop ----

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (victim is null || !victim.IsValid) return HookResult.Continue;

        AddTimer(0.1f, ApplyBotBalance);

        if (_warmupActive) return HookResult.Continue;

        var victimSlot = victim.Slot;
        var isSuicide = attacker is null || !attacker.IsValid
                        || attacker.Slot == victimSlot
                        || @event.Weapon is "worldent" or "world";
        var isKnife = @event.Weapon == "knife";
        var sameTeam = !isSuicide && attacker!.Team == victim.Team;
        var attackerSlot = isSuicide ? victimSlot : attacker!.Slot;

        var result = _engine.OnKill(attackerSlot, victimSlot, sameTeam, isKnife, isSuicide);

        if (result.VictimDemoted)
        {
            victim.Score = _engine.GetProgress(victimSlot).Kills;
            GiveWeaponForIndex(victim, result.VictimNewIndex);
            ChatHelper.ToChatAll($"{Config.Tag}{ChatColors.Gold}{victim.PlayerName}{ChatColors.Darkred} has been demoted!");
        }

        if (!isSuicide && attacker!.IsValid)
        {
            attacker.Score = _engine.GetProgress(attackerSlot).Kills;

            if (result.AttackerWon)
            {
                AnnounceWinner(attacker);
            }
            else if (result.AttackerAdvanced)
            {
                GiveWeaponForIndex(attacker, result.AttackerNewIndex);
                if (Config.WeaponOrder[result.AttackerNewIndex] == "knife") AnnounceAboutToWin(attacker);
                UpdateLeader(attacker.PlayerName);
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ServerRuleset.Apply();
        Server.ExecuteCommand("bot_kick all");

        _engine.Reset();
        _leader.Reset();
        _protected.Clear();

        foreach (var weapon in Utilities.FindAllEntitiesByDesignerName<CWeaponBaseItem>("weapon_"))
            weapon.Remove();

        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, PawnIsAlive: true }))
        {
            player.Score = 0;
            _engine.OnPlayerJoin(player.Slot);
            GiveWeaponForIndex(player, _engine.GetProgress(player.Slot).WeaponIndex);
        }

        AddTimer(0.1f, ApplyBotBalance);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _engine.Reset();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;

        if (Config.Gameplay.SpawnProtection.DropOnAttack) ClearSpawnProtection(player.Slot);

        if (!_engine.Players.TryGetValue(player.Slot, out var progress)) return HookResult.Continue;
        if (progress.WeaponIndex < 0 || progress.WeaponIndex >= Config.WeaponOrder.Length) return HookResult.Continue;

        var weapon = Config.WeaponOrder[progress.WeaponIndex];
        if (weapon is "hegrenade" or "molotov" or "incgrenade")
        {
            var slot = player.Slot;
            var index = progress.WeaponIndex;
            AddTimer(Config.Gameplay.GrenadeRegiveDelaySeconds, () =>
            {
                var p = Utilities.GetPlayerFromSlot(slot);
                if (p is { IsValid: true, PawnIsAlive: true }) GiveWeaponForIndex(p, index);
            });
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
    {
        if (!Config.Gameplay.InfiniteAmmo) return HookResult.Continue;

        var player = @event.Userid;
        if (player is null || !player.IsValid || !player.PawnIsAlive) return HookResult.Continue;

        var weapon = player.ActiveWeapon();
        if (weapon is null) return HookResult.Continue;

        weapon.Clip1 = 250;
        if (Config.Gameplay.InfiniteReserve) weapon.ReserveAmmo[0] = 250;
        return HookResult.Continue;
    }

    // ---- Helpers ----

    private void Greet(CCSPlayerController player)
    {
        ChatHelper.ToChat(player, $"{Config.Tag}Welcome to {Config.Colors.Highlight}GunGame Lite{ChatColors.Default}, {player.PlayerName}!");
        ChatHelper.ToChat(player, $"{Config.Tag}First to knife-kill wins. Every kill upgrades your weapon.");
        ChatHelper.ToChat(player, $"{Config.Tag}Version: {ChatColors.Gold}{ModuleVersion}{ChatColors.Default}");
    }

    private void GiveWeaponForIndex(CCSPlayerController player, int index)
    {
        if (index < 0 || index >= Config.WeaponOrder.Length) return;
        var weapon = Config.WeaponOrder[index];

        AddTimer(0.1f, () =>
        {
            if (!player.IsValid || !player.PawnIsAlive) return;
            player.RemoveWeapons();
            player.GiveNamedItem("item_assaultsuit");
            player.GiveNamedItem("weapon_knife");
            if (weapon != "knife") player.GiveNamedItem($"weapon_{weapon}");
        });
    }

    private void AnnounceAboutToWin(CCSPlayerController player)
    {
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{player.PlayerName}{ChatColors.LightRed} has a knife and is about to win!");
        foreach (var p in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
            HudHelper.PlayerCenterHtml(p,
                $"<font class='fontSize-m' color='white'><span color='#de9b35'>{player.PlayerName}</span><br>has a knife and is about to win!</font>", 3);
    }

    private void AnnounceWinner(CCSPlayerController winner)
    {
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        gameRules.TerminateRound(5.0f, RoundEndReason.RoundDraw);

        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid))
        {
            p.RemoveWeapons();
            if (!p.IsBot)
                HudHelper.PlayerCenterHtml(p,
                    $"<font class='fontSize-m' color='white'>Winner Winner Chicken Dinner</font><br><font class='fontSize-l' color='#2E9F65'>{winner.PlayerName}</font>", 5);
        }

        ChatHelper.ToChatAll($"{Config.Tag}Winner Winner Chicken Dinner: {ChatColors.Gold}{winner.PlayerName}{ChatColors.Default}!");
    }

    private void UpdateLeader(string scorerName)
    {
        var scores = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.IsOnATeam())
            .Select(p => (p.PlayerName, p.Score));

        var top = LeaderTracker.FindTop(scores);
        if (top is null) return;

        var message = _leader.Update(top.Value.name, top.Value.kills, scorerName);
        switch (message.Change)
        {
            case LeaderChange.TookLead:
                var from = message.PreviousLeaderName is null
                    ? ""
                    : $"from {ChatColors.Gold}{message.PreviousLeaderName}{ChatColors.LightRed} ";
                ChatHelper.ToChatAll($"{Config.Tag}{ChatColors.Gold}{message.LeaderName}{ChatColors.LightRed} has taken the lead {from}with {ChatColors.Gold}{message.Kills}{ChatColors.LightRed} points!");
                break;
            case LeaderChange.StillLeading:
                ChatHelper.ToChatAll($"{Config.Tag}{ChatColors.Gold}{message.LeaderName}{ChatColors.LightRed} is still leading with {ChatColors.Gold}{message.Kills}{ChatColors.LightRed} points!");
                break;
        }
    }

    private void ApplyBotBalance()
    {
        int humansT = 0, botsT = 0, humansCt = 0, botsCt = 0;
        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && p.IsOnATeam()))
        {
            var t = p.IsTerrorist();
            if (p.IsBot)
            {
                if (t) botsT++; else botsCt++;
            }
            else
            {
                if (t) humansT++; else humansCt++;
            }
        }

        var balance = BotBalancer.Compute(humansT, botsT, humansCt, botsCt, Config.Bots.QuotaPerTeam, Config.Bots.Enabled);
        for (var i = 0; i < balance.AddT; i++) Server.ExecuteCommand("bot_add t");
        for (var i = 0; i < balance.AddCt; i++) Server.ExecuteCommand("bot_add ct");
        for (var i = 0; i < balance.KickT; i++) Server.ExecuteCommand("bot_kick t");
        for (var i = 0; i < balance.KickCt; i++) Server.ExecuteCommand("bot_kick ct");
    }

    private void ApplySpawnProtection(CCSPlayerController player)
    {
        if (!Config.Gameplay.SpawnProtection.Enabled) return;
        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid) return;

        pawn.TakesDamage = false;
        _protected.Add(player.Slot);

        var slot = player.Slot;
        AddTimer(Config.Gameplay.SpawnProtection.Seconds, () => ClearSpawnProtection(slot));
    }

    private void ClearSpawnProtection(int slot)
    {
        if (!_protected.Remove(slot)) return;
        var pawn = Utilities.GetPlayerFromSlot(slot)?.PlayerPawn.Value;
        if (pawn is not null && pawn.IsValid) pawn.TakesDamage = true;
    }

    private void StartWarmup()
    {
        if (!Config.Gameplay.Warmup.Enabled || _warmupActive) return;
        _warmupActive = true;
        ChatHelper.ToChatAll($"{Config.Tag}Warmup started for {Config.Gameplay.Warmup.Seconds}s — kills don't count yet.");
        AddTimer(Config.Gameplay.Warmup.Seconds, () =>
        {
            _warmupActive = false;
            Server.ExecuteCommand("mp_restartgame 1");
            ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}GunGame starts now!");
        });
    }

    // ---- Admin commands ----

    [ConsoleCommand("css_ggl_restart", "Restarts the GunGame")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRestartCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.ExecuteCommand("mp_restartgame 1");
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller?.PlayerName ?? "Console"}{ChatColors.Default} restarted the game!");
    }

    [ConsoleCommand("css_ggl_bots", "Toggles bots")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnToggleBotsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Config.Bots.Enabled = !Config.Bots.Enabled;
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller?.PlayerName ?? "Console"}{ChatColors.Default} toggled bots to {Config.Bots.Enabled}!");
        AddTimer(0.1f, ApplyBotBalance);
    }

    [ConsoleCommand("css_ggl_reload", "Reloads the plugin")]
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnReloadCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.ExecuteCommand("css_plugins reload GunGameLite");
        ChatHelper.ToChatAll($"{Config.Tag}{Config.Colors.Highlight}{caller?.PlayerName ?? "Console"}{ChatColors.Default} reloaded the plugin!");
    }
}
```

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors, 0 warnings (nullable clean). If the compiler flags any CSS API signature (e.g., `weapon.ReserveAmmo`, `pawn.TakesDamage`, `Team`), fix the exact call it names — these are the only API-drift-sensitive lines.

- [ ] **Step 3: Run the full test suite (unchanged, must stay green)**

Run: `dotnet test -c Release`
Expected: PASS — all engine/config/helper tests green.

- [ ] **Step 4: Commit**

```bash
git add GunGameLite/GunGameLitePlugin.cs
git commit -m "feat: implement GunGameLitePlugin adapter over the tested engine"
```

---

### Task 10: CI workflow, .gitignore, .editorconfig

**Files:**
- Modify: `.github/workflows/build.yml`
- Modify: `.gitignore`
- Create: `.editorconfig`

**Interfaces:**
- Produces: a `test → build → publish` GitHub Actions pipeline on net10; a modern `.gitignore` that keeps `docs/superpowers/` tracked.

- [ ] **Step 1: Replace `.github/workflows/build.yml`**

```yaml
name: Build

on:
  push:
    branches:
      - main
    tags:
      - "*"
  pull_request:
    branches:
      - main

env:
  PROJECT_NAME: "GunGameLite"
  PROJECT_PATH: "./GunGameLite/GunGameLite.csproj"
  OUTPUT_PATH: "./GunGameLite/GunGameLite"
  PUBLISH_PATH: "./GunGameLite/GunGameLite"
  DOTNET_VERSION: "10.0.x"

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - name: Restore
        run: dotnet restore
      - name: Test
        run: dotnet test -c Release --no-restore

  build:
    runs-on: ubuntu-latest
    needs: test
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build ${{ env.PROJECT_PATH }} -c Release -o ${{ env.OUTPUT_PATH }}

  publish:
    if: startsWith(github.ref, 'refs/tags/')
    permissions: write-all
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build ${{ env.PROJECT_PATH }} -c Release -o ${{ env.OUTPUT_PATH }}
      - name: Clean files
        run: |
          rm -f \
            ${{ env.OUTPUT_PATH }}/GunGameLite.deps.json \
            ${{ env.OUTPUT_PATH }}/GunGameLite.pdb
      - name: Zip
        uses: thedoctor0/zip-release@0.7.5
        with:
          type: 'zip'
          filename: '${{ env.PROJECT_NAME }}.zip'
          path: ${{ env.PUBLISH_PATH }}
      - name: Create Release
        uses: ncipollo/release-action@v1.14.0
        with:
          artifacts: "${{ env.PROJECT_NAME }}.zip"
          name: "${{ github.ref_name }}"
          tag: "${{ github.ref_name }}"
          allowUpdates: true
          body: |
            Place the plugin in game/csgo/addons/counterstrikesharp/plugins/GunGameLite
```

- [ ] **Step 2: Replace `.gitignore`**

```gitignore
# Rider / IDE
.idea/
.vs/
*.user

# .NET build output
**/bin/
**/obj/

# OS
.DS_Store
Thumbs.db

# Local publish
*.zip
```

Note: `docs/` is intentionally NOT ignored — the specs and plans stay tracked.

- [ ] **Step 3: Create `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space

[*.cs]
indent_size = 4
csharp_new_line_before_open_brace = all
dotnet_sort_system_directives_first = true
csharp_prefer_braces = true:suggestion

[*.{json,yml,yaml,csproj,props}]
indent_size = 2
```

- [ ] **Step 4: Verify build/test still pass and yaml is well-formed**

Run: `dotnet build -c Release && dotnet test -c Release`
Expected: PASS.
Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/build.yml')); print('yaml ok')"`
Expected: `yaml ok`.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/build.yml .gitignore .editorconfig
git commit -m "ci: net10 test/build/publish pipeline; modernize gitignore + editorconfig"
```

---

### Task 11: README, CLAUDE.md, LICENSE, credits

**Files:**
- Modify: `README.md` (full rewrite)
- Modify: `CLAUDE.md`
- Modify: `LICENSE` (replace with GPL-3.0 full text)

**Interfaces:**
- Produces: user-facing docs matching the shipped config and commands.

- [ ] **Step 1: Replace `LICENSE` with the GPL-3.0 text**

Fetch the canonical text and write it verbatim:
```bash
curl -fsSL https://www.gnu.org/licenses/gpl-3.0.txt -o LICENSE
head -1 LICENSE   # expect: "                    GNU GENERAL PUBLIC LICENSE"
```
If offline, copy the GPL-3.0 text from `creazy231/cs2-css-flashlight`'s `LICENSE` (same content).

- [ ] **Step 2: Replace `README.md`**

````markdown
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
| `Tag` | `[GGL]` | Chat prefix (supports `{COLOR}` tokens). |
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
````

- [ ] **Step 3: Replace `CLAUDE.md`**

```markdown
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
```

- [ ] **Step 4: Bump version note**

Confirm `ModuleVersion => "0.1.0"` in `GunGameLite/GunGameLitePlugin.cs` (set in Task 9). No change needed if already `0.1.0`.

- [ ] **Step 5: Commit**

```bash
git add README.md CLAUDE.md LICENSE
git commit -m "docs: rewrite README, update CLAUDE.md, adopt GPL-3.0"
```

---

### Task 12: Final verification

**Files:** none (verification only).

- [ ] **Step 1: Clean build**

Run: `dotnet clean && dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Full test run with counts**

Run: `dotnet test -c Release`
Expected: PASS; total tests ≈ 40+ across `WeaponProgressionTests`, `GunGameEngineTests`, `BotBalancerTests`, `SpawnProtectionTests`, `LeaderTrackerTests`, `ConfigValidationTests`.

- [ ] **Step 3: Structural sanity checks**

Run:
```bash
test ! -f WeaponManagerWeapons.cs && echo "dead code removed"
! grep -rn "Console.WriteLine" GunGameLite/ && echo "no Console.WriteLine in plugin"
grep -q "net10.0" GunGameLite/GunGameLite.csproj && echo "net10 target ok"
grep -q "GNU GENERAL PUBLIC LICENSE" LICENSE && echo "GPL license ok"
```
Expected: all four confirmations print.

- [ ] **Step 4: Confirm the plugin output name**

Run: `ls GunGameLite/bin/Release/net10.0/GunGameLite.dll && echo "dll name ok"`
Expected: the DLL exists and is named `GunGameLite.dll`.

- [ ] **Step 5: Final commit (if anything changed) & summary**

```bash
git status
git log --oneline modernize-gungame-net10 ^main
```
Expected: a clean, linear history of the tasks above on `modernize-gungame-net10`.

---

## Self-Review

**Spec coverage:** net10/1.0.371 (Task 1), engine + progression/kill matrix/catch-up (Tasks 2–3), bots (Task 4), spawn protection (Task 5 policy + Task 9 wiring), leader (Task 6), config v2 + Validate (Task 7), helpers/ruleset (Task 8), plugin lifecycle/commands/warmup/ammo/consumables (Task 9), CI/gitignore/editorconfig (Task 10), README/CLAUDE/LICENSE/version (Task 11), verification incl. dead-code + no-Console checks (Task 12). Team-kill decision ① in engine + config; GPL-3.0 decision ② in Task 11. All spec sections map to a task.

**Placeholder scan:** No TBD/TODO; every code step contains complete code; every command has expected output.

**Type consistency:** `OnKill(int,int,bool,bool,bool)`, `KillResult` fields, `BotBalance` record, `LeaderMessage`/`LeaderChange`, `GunGameLiteConfig`/`Gameplay*` names, and `GunGameLitePlugin.Instance` are used identically across Tasks 3/4/6/7/8/9.
