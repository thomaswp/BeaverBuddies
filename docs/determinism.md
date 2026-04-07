# Determinism and Desync Detection

BeaverBuddies uses a **lockstep simulation** model: both the server and client execute the same game logic with the same inputs and must produce identical results. This page explains how determinism is enforced and how desyncs are detected when it fails.

## Why Determinism Matters

Timberborn is a single-player game. Many of its internal systems use patterns that are inherently non-deterministic:

- **Random number generation** is used throughout gameplay logic (resource reproduction, beaver behavior, GUID creation) but also in UI/audio code. Without control, these share the same RNG state, causing divergence.
- **Rendering and audio** systems call into game code during `Update()` cycles, potentially mutating shared state.
- **Parallel tick execution** processes water simulation and other systems on background threads, which can execute in non-deterministic order.
- **Time.time** varies between machines based on frame rate and real-world timing.

If any of these cause the server and client to reach a different game state, the simulation diverges -- a **desync**. BeaverBuddies addresses each of these through `DeterminismService` and a collection of Harmony patches.

## DeterminismService

**File:** `BeaverBuddies/DeterminismService.cs`

`DeterminismService` is the central coordinator for all determinism enforcement. It is a `RegisteredSingleton` bound during game scene configuration.

### RNG Seeding

Both server and client must start with identical RNG state. The static method `InitGameStartState(byte[] mapBytes)` computes a hash of the save file bytes and uses it to seed Unity's random number generator:

```csharp
public static void InitGameStartState(byte[] mapBytes)
{
    int state = 13;
    for (int i = 0; i < mapBytes.Length; i++)
    {
        state = TimberNetBase.CombineHash(state, mapBytes[i]);
    }
    // Stores the seed; applied when DeterminismService is constructed
    nextSeedOnLoad = state;
}
```

The seed is stored in `nextSeedOnLoad` and applied via `UnityEngine.Random.InitState()` when the `DeterminismService` constructor runs during scene load. Both server (`ServerHostingUtils.LoadAndHost`) and client (`ClientConnectionService.LoadMap`) call `InitGameStartState` with identical map bytes before loading, guaranteeing the same initial RNG state.

### Removing Non-Deterministic Singletons from the Tick Loop

Many Timberborn systems use `UpdateSingleton()` to run logic every frame. Some of these consume random numbers for non-gameplay purposes (sound effects, UI previews, analytics). BeaverBuddies intercepts these with Harmony patches that mark them as "non-game" code:

| Patcher Class | Target | Purpose |
|---|---|---|
| `InputPatcher` | `InputService.UpdateSingleton` | Input processing uses RNG |
| `SoundsPatcher` | `Sounds.GetRandomSound` | Sound selection |
| `SoundEmitterPatcher` | `SoundEmitter.Update` | Sound playback |
| `DateSalterPatcher` | `DateSalter.GenerateRandomNumber` | Analytics salting |
| `PlantableDescriberPatcher` | `PlantableDescriber.GetPreviewFromTemplate` | UI preview generation |
| `StockpileGoodPileVisualizerPatcher` | `StockpileGoodPileVisualizer.Awake` | Visual randomization |
| `RecoveredGoodStackFactoryPatcher` | `RecoveredGoodStackFactory.RandomizeRotation` | Visual randomization |
| `LoopingSoundPlayerPatcher` | `LoopingSoundPlayer.PlayLooping` | Audio looping |
| `BotManufactoryAnimationControllerPatcher` | `BotManufactoryAnimationController.ResetRingRotation` | Animation |
| `TerrainBlockRandomizerPickVariationPatcher` | `TerrainBlockRandomizer.PickVariation` | Terrain visuals |
| `BeaverTextureSetterStartPatcher` | `BeaverTextureSetter.Start` | Beaver appearance |

Each patcher calls `DeterminismService.SetNonGamePatcherActive(type, true)` in its `Prefix` and `false` in its `Postfix`. While any non-game patcher is active, `ShouldFreezeSeed` returns `true`, diverting RNG calls to a separate `System.Random` instance that does not affect the game's deterministic state.

### NonTickRandomNumberGenerator

For a more comprehensive approach, `ParameterProviderPatch` intercepts Bindito's dependency injection. When a class in the `blacklist` (e.g., `BeaverTextureSetter`, `GameMusicPlayer`, `Sounds`) requests an `IRandomNumberGenerator`, it receives a `NonTickRandomNumberGenerator` wrapper instead. This wrapper calls `DeterminismService.GetNonGameRandom()` around every RNG operation, ensuring these classes never touch the gameplay RNG.

### ShouldFreezeSeed Logic

The `ShouldFreezeSeed` property determines whether the current random call should use the non-game RNG:

1. If `EventIO.IsNull` (not in multiplayer): return `false` -- no freezing needed.
2. If `IsNonGameplay` flag is set: return `true`.
3. If the game is still loading (`!ReplayService.IsLoaded`): return `false` -- loading RNG is gameplay-critical.
4. If on a non-Unity thread: return `true` -- background threads should not touch game RNG.
5. If any non-game patchers are active: return `true`.
6. If currently ticking (`IsTicking`): return `false` -- tick logic is gameplay.
7. If replaying events (`ReplayService.IsReplayingEvents`): return `false`.
8. Otherwise: log a warning and return `true` (assume non-game).

### GUID Determinism

`GuidPatcher` patches `Guid.NewGuid()` to generate GUIDs using Unity's (seeded) random number generator instead of the system's cryptographic RNG. This ensures entity IDs are identical on both machines. The method `GenerateWithUnityRandom()` fills 16 bytes from `UnityEngine.Random.Range`.

### Time.time Determinism

`TimeTimePatcher` patches `Time.time` to return a deterministic value based on `ticksSinceLoad * Time.fixedDeltaTime`, removing frame-rate-dependent variation.

### Entity Instantiation

`EntityComponentInstantiatePatcher` ensures that when entities are created, ticking is interrupted (`TickingService.ShouldInterruptTicking = true`) so the new entity's `Start()` runs before the next bucket ticks. It also guards against duplicate GUIDs.

### DayNightCycle Fix

`DayNightCycleFluidSecondsPassedTodayPatcher` removes the frame-interpolated `_secondsPassedThisTick` from the day/night cycle calculation, making it purely tick-based.

### Save Determinism

`GameSaverSavePatcher` ensures saves only happen after a full tick completes by calling `TickingService.FinishFullTickAndThen()`, preventing saves mid-tick that could capture inconsistent state.

## Fixes Directory

The `BeaverBuddies/Fixes/` directory contains targeted patches for specific determinism problems.

### AnimationFixes.cs

**Problem:** `MovementAnimator.Update()` uses `Time.time` to interpolate character positions. Since rendering happens at different rates on different machines, character positions would diverge.

**Solution:** `AnimatedPathFollowerUpdatePatcher` replaces the original `Update` method. Instead of using real `Time.time`, it calculates an interpolated time based on the `TickProgressService`:

```csharp
time = tickProgressService.TimeAtLastTick(entity) +
    Time.fixedDeltaTime * tickProgressService.PercentTicked(entity);
```

This makes animations tick-based while still appearing smooth between ticks. Before each tick, `TEBPatcher` resets animated positions to match the deterministic `PathFollower` position, ensuring the tick starts from a known state.

### WaterSourceFix.cs (LateTickableBuffer)

**Problem:** `WaterSource.Tick()` executes during the parallel tick phase, where execution order across threads is non-deterministic.

**Solution:** `LateTickableBuffer` intercepts `WaterSource.Tick()` calls during the parallel phase and buffers them. After `TickableSingletonService.FinishParallelTick()` completes, the buffered ticks execute sequentially on the main thread:

```csharp
[HarmonyPatch(typeof(TickableSingletonService),
    nameof(TickableSingletonService.FinishParallelTick))]
class TickableSingletonServiceFinishParallelTickPatcher
{
    public static void Postfix()
    {
        var buffer = SingletonManager.GetSingleton<LateTickableBuffer>();
        buffer?.TickComponents();
    }
}
```

The `LateTickableBuffer.TickingLate` property tracks whether the buffer is actively ticking, so the `WaterSource.Tick` prefix knows when to allow the original method through.

### WaterWheelFix.cs

**Problem:** `MechanicalNodeFacingMarkerDrawer.GetTransput()` can throw exceptions during UI rendering when water wheel transput calculations encounter edge cases.

**Solution:** A Harmony patch wraps the call in a try/catch, returning `null` on failure. This is a UI-only fix that prevents crashes without affecting determinism.

### TickOnlyArrayFix.cs

**Problem:** `TickOnlyArrayService.AllowEdit` returns `false` outside of tick execution. During deterministic saves, code needs to read from tick-only arrays, but the API conflates read and write permission.

**Solution:** `TickOnlyArrayServiceAllowEditPatch` returns `true` when `GameSaveHelper.IsSavingDeterministically` or `GameSaverSavePatcher.IsSaving` is active, allowing array access during save operations.

## Desync Detection

The `BeaverBuddies/DesyncDetecter/` directory implements two levels of desync detection.

### RNG State Check (Lightweight)

Every `ReplayEvent` carries an optional `randomS0Before` field -- the `s0` component of Unity's RNG state at the time the event was recorded. When the other side replays the event, `ReplayService` compares its current `s0` to the recorded value:

```csharp
if (!Settings.Debug && replayEvent.randomS0Before != null)
{
    int s0 = UnityEngine.Random.state.s0;
    int randomS0Before = (int)replayEvent.randomS0Before;
    if (s0 != randomS0Before)
    {
        Plugin.LogWarning($"Random state mismatch: {s0:X8} != {randomS0Before:X8}");
        HandleDesync();
        break;
    }
}
```

This lightweight check catches most desyncs without the overhead of full tracing. It is always active (when not in Debug mode, which uses the more detailed trace system instead).

### DesyncDetecterService (Detailed Tracing)

When `Settings.Debug` (or `Settings.TemporarilyDebug`) is enabled, `DesyncDetecterService` records a detailed per-tick trace of game events.

**Key types:**

- **`Trace`** -- a serializable struct with `message` (what happened) and `stackTrace` (where it happened).
- **`DesyncDetecterService`** -- static service that maintains a rolling buffer of traces (up to `maxTraceTicks = 10` ticks of history).

**Workflow:**

1. `StartTick(int tick)` is called from `ReplayService` at the start of each tick, creating a new trace list.
2. Throughout the tick, `Trace(string message)` is called from various Harmony patches to record significant events.
3. At the end of each tick, `CreateReplayEventsAndClear()` packages traces into `TraceLoggedForTickEvent` events, which are sent from server to client.
4. On the client, `TraceLoggedForTickEvent.Replay()` calls `VerifyTraces()` to compare the server's traces against the client's.
5. If a mismatch is found, `VerifyTraces` builds a detailed log showing shared history, the point of divergence, and both sides' traces at that point.

### TraceLoggedForTickEvent

This `ReplayEvent` subclass carries a tick number and a list of `Trace` objects. Note that `tick` refers to the tick being traced, while `ticksSinceLoad` (inherited from `ReplayEvent`) refers to when the event was actually sent (usually one tick later).

### DesyncPatches

**File:** `BeaverBuddies/DesyncDetecter/DesyncPatches.cs`

This file contains Harmony patches that add trace points to critical game systems. These are only active when `Settings.Debug` is true:

| Patch | What It Traces |
|---|---|
| `NRPMarkSpotsPatcher` | `NaturalResourceReproducer.MarkSpots` -- resource reproduction spot tracking |
| `NRPUnmarkSpotsPatcher` | `NaturalResourceReproducer.UnmarkSpots` -- spot removal |
| `SpawnValidationServiceCanSpawnPatcher` | Whether a resource can spawn at given coordinates |
| `NRRPatcher` | `NaturalResourceReproducer.SpawnNewResources` -- actual spawning |
| `WalkerFindPathPatcher` | `Walker.FindPath` -- beaver pathfinding decisions |
| `NaturalResourceModelRandomizerPatcher` | Diameter scale randomization |
| `WalkToReservableExecutorPatcher` | Reservable walking targets |
| `WateredNaturalResourceStartDryingOutPatcher` | Drying-out timer creation |
| `SoilMoistureMapSetMoistureLevelPatcher` | Soil moisture level hashes |
| `ThreadSafeWaterMapUpdateDataPatcher` | Water map column and count hashes |
| `TEBAddPatcher` | Entity additions to tick buckets |

## Common Desync Causes

Based on the development notes in `DeterminismService.cs`, these are the most common causes of desync:

1. **Non-saved random initialization** -- Components like `WateredNaturalResource.Awake()` use random numbers before the client has received its RNG seed. Fixed by seeding from the map hash.

2. **Floating-point accumulation** -- Movement and time calculations that accumulate small differences over many frames. Fixed by making `Time.time` and animations tick-based.

3. **OnDestroyed timing** -- Gameplay code in `OnDestroyed` callbacks can fire at different times on different machines (end of frame vs. during tick).

4. **Parallel tick ordering** -- Systems like water sources that execute during parallel ticks may process in different orders. Fixed by `LateTickableBuffer`.

5. **Frame-dependent updates** -- Any code that runs per-frame rather than per-tick and modifies game state. Fixed by moving `RecoveredGoodStackSpawner.UpdateSingleton` and similar systems to tick-only execution via `TickReplacerService`.

6. **GUID collisions** -- During preloading, newly generated GUIDs could collide with existing save data. Fixed by the collision-check loop in `EntityComponentInstantiatePatcher`.
