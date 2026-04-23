# Tick Synchronization

BeaverBuddies must ensure that the server and all clients produce identical game state on every tick. This page documents how the tick synchronization system works, from the central `ReplayService` orchestrator down to the low-level tick loop patching in `TickingService`.

## The Synchronization Problem

Timberborn uses a **bucket-based tick system**. Rather than updating all entities once per frame, the game divides entities into buckets and updates a variable number of buckets per frame depending on game speed and frame rate. A single "tick" consists of updating all buckets (plus singleton tick services) exactly once.

For multiplayer to work, both server and client must:

1. Execute the same set of events at the same tick boundaries.
2. Maintain identical random number generator state.
3. Process entities in the same order.

The core challenge is that user actions (placing buildings, changing settings, etc.) can happen at any point during a frame, but they must be applied at deterministic tick boundaries. The `ReplayService` solves this by capturing events, deferring them to tick boundaries, and replaying them in a consistent order on all peers.

---

## ReplayService

`ReplayService` (`BeaverBuddies/ReplayService.cs`) is the central orchestrator for multiplayer synchronization. It implements `IReplayContext`, `IPostLoadableSingleton`, `IUpdatableSingleton`, and `IResettableSingleton`.

### Key Fields

| Field | Type | Purpose |
|---|---|---|
| `ticksSinceLoad` | `int` | Monotonically increasing tick counter, set via a property that also updates `TimeTimePatcher` and `DesyncDetecterService` |
| `IsReplayingEvents` | `static bool` | True while events are being replayed; prevents re-recording events during replay |
| `IsDesynced` | `bool` | Set to true when a desync is detected; disables further synchronization |
| `eventsToSend` | `ConcurrentQueue<ReplayEvent>` | Events waiting to be sent to connected peers |
| `eventsToPlay` | `ConcurrentQueue<ReplayEvent>` | Events queued locally for deferred execution (server's `QueuePlay` behavior) |
| `TargetSpeed` | `float` | The user's desired game speed, distinct from the actual speed which may be adjusted for catch-up |
| `io` | `EventIO` | Reference to the current IO implementation (server, client, or file) via `EventIO.Get()` |
| `IsLoaded` | `static bool` | Whether the service has finished initialization |

### Initialization

After the game scene loads, `ReplayService` waits 2 update frames (`waitUpdates`) before calling `Initialize()`. This delay ensures Timberborn's own random initialization (tree lifespans, etc.) has completed. `Initialize()` sets `IsLoaded = true` and starts the desync detection system.

### DoTick()

Called at the very start of each tick (before any entity updates), via `TickingService`:

```csharp
public void DoTick()
{
    // 1. If server in debug mode, capture desync traces from prior tick
    // 2. Increment ticksSinceLoad
    // 3. If server, enqueue a HeartbeatEvent
    // 4. Call DoTickIO() to replay and send events
    // 5. Update speed (pause if out of events, speed up if behind)
    // 6. After tick 1, stop accepting new clients (server only)
}
```

The tick counter is incremented before `DoTickIO()`, so events are processed at the new tick number.

### DoTickIO()

The main synchronization method, called from both `DoTick()` and `UpdateSingleton()` (when paused):

```csharp
private void DoTickIO()
{
    ReplayEvents();
    SendEvents();
}
```

### ReplayEvents()

Reads and executes pending events for the current tick:

```
1. Read events from IO for current tick
   -> io.ReadEvents(TicksSinceLoad)
   -> Flattens any GroupedEvent into individual events

2. Dequeue locally queued events (eventsToPlay)
   -> Sets their ticksSinceLoad to current tick
   -> Appends to the replay list

3. Set IsReplayingEvents = true

4. For each event:
   a. Check RNG state (randomS0Before) if not in Debug mode
      -> If mismatch, call HandleDesync() and stop
   b. Record current random state (s0) on the event
   c. Call event.Replay(this)
   d. If recording is not skipped, enqueue for sending

5. Set IsReplayingEvents = false
```

The RNG state check is the primary desync detection mechanism. Each event records `randomS0Before` -- the Unity random state just before the event was first played on the server. When the client replays the same event, it verifies its own random state matches. A mismatch means the game states have diverged.

### SendEvents()

Groups all pending events and sends them to connected peers:

```csharp
private void SendEvents()
{
    // Drain eventsToSend queue into a list
    // Stamp each event with ticksSinceLoad
    // Wrap in a GroupedEvent
    // Call EventIO.Get().WriteEvents(group)
}
```

Events are grouped into a single `GroupedEvent` per tick to ensure all events for a tick arrive together. This prevents a client from seeing a partial tick's events and starting to process them prematurely.

### RecordEvent()

Called by Harmony patches when the user performs an action:

```csharp
public void RecordEvent(ReplayEvent replayEvent)
{
    // Skip if currently replaying (would cause re-recording)
    // Skip if not loaded yet

    switch (io.UserEventBehavior):
        QueuePlay -> eventsToPlay.Enqueue(replayEvent)
        Send      -> eventsToSend.Enqueue(replayEvent)
}
```

- **Server (QueuePlay):** The event goes into `eventsToPlay` and will be replayed at the next tick boundary via `ReplayEvents()`. This ensures the event executes at the same tick on server and clients.
- **Client (Send):** The event goes into `eventsToSend` and is transmitted to the server without local execution. The server will assign a tick and echo it back.

### Speed Management

`ReplayService` manages game speed to keep clients synchronized:

```csharp
private void UpdateSpeed()
{
    // If out of events: pause (speed = 0)
    // If behind by more ticks than target speed:
    //   speed up to min(ticksBehind, 10)
    // Otherwise: set speed to TargetSpeed
}
```

- `TargetSpeed` is the user's requested speed (set via `SetTargetSpeed()`).
- If the client falls behind (more received events than processed), the game speeds up to catch up, capped at speed 10.
- If the client runs out of events (server hasn't sent heartbeats for future ticks yet), the game pauses.
- Speed changes use `SpeedChangePatcher.SetSpeedSilentlyNow()` to avoid triggering the speed-change event patches.

### Desync Handling

When `HandleDesync()` is called:

1. Sets `IsDesynced = true` to stop further sync attempts.
2. Creates a `ClientDesyncedEvent` with debug information.
3. Sends it to the server immediately.
4. Pauses the game.
5. Resets the `EventIO` (disconnects).

### GroupedEvent

```csharp
class GroupedEvent : ReplayEvent
{
    public List<ReplayEvent> events;
}
```

A container event that wraps multiple events for a single tick. When received, `ReadEventsFromIO()` flattens grouped events back into individual events before replay. `GroupedEvent.Replay()` throws `NotImplementedException` -- it must never be replayed directly.

### HeartbeatEvent

```csharp
class HeartbeatEvent : ReplayEvent
{
    public override void Replay(IReplayContext context) { }  // no-op
}
```

A no-op event sent by the server each tick. Its purpose is to signal clients that a tick has occurred, even if no player actions happened. Without heartbeats, clients would have no events for that tick and would pause indefinitely.

---

## TickingService

`TickingService` (`BeaverBuddies/ReplayService.cs`, same file) controls how the game's tick loop operates during multiplayer. It replaces Timberborn's default `TickableBucketService.TickBuckets` method via a Harmony prefix patch.

### The Patching Mechanism

```
[HarmonyPatch(typeof(TickableBucketService), nameof(TickableBucketService.TickBuckets))]
static class TickableBucketServiceTickUpdatePatcher
{
    static bool Prefix(TickableBucketService __instance, int numberOfBucketsToTick)
    {
        if (EventIO.IsNull) return true;  // no multiplayer, use default
        TickingService ts = GetSingleton<TickingService>();
        if (ts == null) return true;
        return ts.TickBuckets(__instance, numberOfBucketsToTick);
    }
}
```

When multiplayer is active, the patch returns `false` from the prefix, completely replacing the original method with `TickingService.TickBuckets()`.

### TickBuckets -- The Core Loop

The replacement tick loop:

```
while (ShouldTick(__instance, numberOfBucketsToTick--)):
    if (TickReplayServiceOrNextBucket(__instance)):
        numberOfBucketsToTick++  // refund the bucket if we ticked ReplayService
OnTickingCompleted()
```

### ShouldTick Decision Logic

`ShouldTick()` determines whether to continue processing buckets:

```
1. If ShouldInterruptTicking is set -> stop
2. If TargetSpeed == 0 (user paused) -> stop
3. If at start of tick AND ReplayService not ready -> stop
   (client waiting for server heartbeat)
4. If ShouldCompleteFullTick is set -> continue until bucket index wraps to 0
5. Otherwise -> continue while numberOfBucketsToTick > 0
```

### TickReplayServiceOrNextBucket

This method injects `ReplayService.DoTick()` at the start of each tick cycle:

```
if at start of tick (bucket index == 0):
    if haven't ticked ReplayService yet:
        Finish any parallel tick operations
        Call replayService.DoTick()
        Set HasTickedReplayService = true
        Return true (refund bucket)
    else:
        Reset HasTickedReplayService = false

Tick the next bucket normally
Update NextBucket
Return false
```

This ensures that:
- `ReplayService.DoTick()` runs exactly once per tick cycle, before any entity buckets.
- Parallel tick operations from the previous tick are completed before the replay service runs.
- The replay service tick does not consume one of the allocated bucket slots.

### Key Properties

| Property | Type | Purpose |
|---|---|---|
| `ShouldInterruptTicking` | `bool` | When true, stops ticking ASAP (resets each frame via `OnTickingCompleted`) |
| `ShouldCompleteFullTick` | `bool` | When true, forces ticking to continue until the tick cycle completes |
| `HasTickedReplayService` | `bool` | Tracks whether `DoTick()` has been called this tick cycle |
| `NextBucket` | `int` | The current bucket index, exposed for other services |

### FinishFullTickAndThen

```csharp
public void FinishFullTickAndThen(Action value)
{
    onCompletedFullTick.Add(value);
    ShouldCompleteFullTick = true;
}
```

Schedules a callback to run after the current tick completes. Used by `ReplayService.FinishFullTickIfNeededAndThen()` to ensure actions that need a consistent game state (like saving) happen at tick boundaries.

### IEarlyTickableSingleton

```csharp
public interface IEarlyTickableSingleton : ITickableSingleton { }
```

A marker interface. The `TickableSingletonServicePatcher` Harmony patch reorders the singleton tick list so that any `IEarlyTickableSingleton` implementations are ticked first, before other tickable singletons. This ensures certain services run at the very beginning of each tick's singleton phase.

---

## Supporting Services

### TickProgressService

`TickProgressService` (`BeaverBuddies/TickProgressService.cs`) provides information about how far through a tick the game currently is. This is useful for smooth visual interpolation between discrete tick updates.

**Key methods:**

- **`HasTicked(EntityComponent)`** -- Returns whether a given entity has already been updated in the current partial tick. Uses the entity's bucket index compared to the current bucket index. Handles the edge case where `_nextBucketIndex == 0` (either just ticked the replay service or about to).
- **`PercentTicked(EntityComponent)`** -- Returns a `float` (0.0 to 1.0) representing how many buckets have ticked since this entity's bucket. Used for animation interpolation.
- **`TimeAtLastTick(EntityComponent)`** -- Returns `Time.time` if the entity has ticked this frame, or `Time.time - Time.fixedDeltaTime` if not. Useful for time-based interpolation.
- **`GetEntityBucketIndex(EntityComponent)`** -- Looks up which bucket an entity belongs to via `TickableBucketService.GetEntityBucketIndex()`.

### TickReplacerService

`TickReplacerService` (`BeaverBuddies/TickReplacerService.cs`) moves certain frame-based updates to tick-based updates for determinism.

Some Timberborn systems perform work in `UpdateSingleton()` (called every frame) that has gameplay effects. Since frame rate varies between machines, these updates must be moved to `Tick()` for deterministic multiplayer. Currently handles:

- **`RecoveredGoodStackSpawner`** -- Normally spawns recovered goods during its `UpdateSingleton`. The original update is suppressed by a Harmony patch (`RecoveredGoodStackSpawnerUpdateSingletonPatcher`), and `TickReplacerService.Tick()` calls the base update behavior instead, ensuring spawning happens at deterministic tick boundaries.

### TickWathcerService

`TickWathcerService` (`BeaverBuddies/TickWatcherService.cs`) is a simple `ITickableSingleton` that counts ticks and tracks time. It provides:

- **`TicksSinceLoad`** -- Counter incremented each tick.
- **`TotalTimeInFixedSeconds`** -- Derived from `IDayNightCycle.HoursPassedToday`, converts the in-game time to seconds.

Note: This service predates the tick counting in `ReplayService` and appears to be largely superseded by it. The `ReplayService` manages its own `ticksSinceLoad` counter that serves as the authoritative tick count for synchronization.

---

## Tick Lifecycle Summary

A complete tick cycle during multiplayer proceeds as follows:

```
Frame starts
  |
  v
TickableBucketService.TickBuckets() called by Timberborn
  |
  v
[Harmony Prefix] -> TickingService.TickBuckets() takes over
  |
  v
ShouldTick() check passes
  |
  v
Bucket index == 0, ReplayService not yet ticked
  |
  +-- Finish parallel ticks from prior tick
  +-- ReplayService.DoTick()
  |     |
  |     +-- ticksSinceLoad++
  |     +-- Enqueue HeartbeatEvent (server only)
  |     +-- DoTickIO()
  |     |     +-- ReplayEvents()
  |     |     |     +-- Read events from IO
  |     |     |     +-- Dequeue local events
  |     |     |     +-- Verify RNG state per event
  |     |     |     +-- Execute each event
  |     |     |     +-- Record successful events for sending
  |     |     +-- SendEvents()
  |     |           +-- Group events into GroupedEvent
  |     |           +-- Write to EventIO
  |     +-- UpdateSpeed()
  |     +-- StopAcceptingClients() (server, tick 1 only)
  |
  v
HasTickedReplayService = false (on next bucket-0 entry)
  |
  v
Normal entity bucket ticking proceeds...
  Bucket 0 -> Bucket 1 -> ... -> Bucket N-1
  |
  v
ShouldTick() returns false (all allocated buckets processed)
  |
  v
OnTickingCompleted()
  +-- Reset ShouldInterruptTicking
  +-- Execute FinishFullTickAndThen callbacks (if any)
  |
  v
Frame ends
```

### Client Tick Gating

The client has an additional gate: `IsReadyToStartTick` checks whether events exist for the next tick (`TicksSinceLoad + 1`). If the server has not yet sent a heartbeat for that tick, `ShouldTick()` returns false and the client waits. Combined with the speed management in `UpdateSpeed()` (which pauses when `IsOutOfEvents` is true), this ensures clients never run ahead of the server.
