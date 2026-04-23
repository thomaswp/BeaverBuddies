# Architecture Overview

This page describes the high-level architecture of BeaverBuddies, a multiplayer co-op mod for Timberborn.

## Architecture Diagram

```
+----------------------------------------------------------+
|                      Plugin (IModStarter)                 |
|          Harmony.PatchAll() + ApplyAutomationPatches()    |
+----+---------------------------+-------------------------+
     |                           |
     v                           v
+--------------------+   +---------------------------+
| ReplayConfigurator |   | ConnectionMenuConfigurator|
| [Context("Game")]  |   | [Context("MainMenu")]     |
+----+---------------+   +---------------------------+
     |                        |
     | (only if EventIO       | Registers:
     |  is set)               |  ClientConnectionService
     v                        |  ClientConnectionUI
+--------------------+        |  FirstTimerService
| Co-op Services     |        |  ConfigIOService
|  ReplayService     |        |  MultiplayerMapMetadataService
|  TickingService    |        |  SteamOverlayConnectionService
|  DeterminismService|        |  Settings
|  TickProgressSvc   |        |
|  TickReplacerSvc   |        |
|  RehostingService  |        |
|  ReportingService  |        |
|  DesyncDetecterSvc |        |
+----+---------------+        |
     |                        |
     v                        v
+--------------------+   SingletonManager.Reset()
| EventIO (interface)|   (called by both configurators)
+----+----------+----+
     |          |
     v          v
+---------+  +-----------+
|ServerIO |  | ClientIO  |
+---------+  +-----------+
     |          |
     v          v
+-------------------------+
| TimberNet               |
| (TimberServer /         |
|  TimberClient)          |
| TCP + Steam transports  |
+-------------------------+
```

## Project Structure

```
BeaverBuddies/
  Connect/          Client/server connection services, UI for joining/hosting
  DesyncDetecter/   Desync detection and tracing (hash-based state comparison)
  Editor/           In-game editor tools (debug/development utilities)
  Events/           All ReplayEvent subclasses (ToolEvents, TimeEvents, etc.)
  Fixes/            Harmony patches that fix Timberborn determinism issues
  IO/               EventIO interface and implementations (ServerEventIO,
                    ClientEventIO, FileWriteIO, FileReadIO, NetIOBase)
  MultiStart/       Multi-instance launch support for testing
  Reporting/        Desync bug reporting service (uploads logs/saves)
  Steam/            Steam networking integration (overlay invites, P2P sockets)
  Util/             Shared utilities, logging, reflection helpers
```

Key root-level files in the project:

| File | Purpose |
|------|---------|
| `Plugin.cs` | Mod entry point, Harmony patching, configurators |
| `SingletonManager.cs` | Custom singleton registry with reset support |
| `ReplayService.cs` | Core orchestrator for event recording and replay |
| `DeterminismService.cs` | Validates game state stays in sync across clients |
| `TickingService.cs` | Controls tick progression and speed synchronization |
| `TickProgressService.cs` | Tracks tick progress for UI feedback |
| `TickReplacerService.cs` | Replaces Timberborn's tick system for co-op control |
| `Settings.cs` | Mod configuration (port, debug mode, etc.) |

## Plugin Entry Point

The mod starts in `Plugin.cs`, which implements `IModStarter`:

```csharp
[HarmonyPatch]
public class Plugin : IModStarter
{
    public void StartMod(IModEnvironment modEnvironment)
    {
        logger = new UnityLogger();
        Log($"{Name} v{Version} is loaded!");

        Harmony harmony = new Harmony(ID);
        harmony.PatchAll();                              // Apply all [HarmonyPatch] attributes
        AutomationEvent.ApplyAutomationPatches(harmony); // Apply reflection-based patches
    }
}
```

`Harmony.PatchAll()` scans the assembly for all classes decorated with `[HarmonyPatch]` and applies their prefix/postfix methods. `AutomationEvent.ApplyAutomationPatches()` handles a separate set of patches that are generated dynamically at runtime via reflection (see [Event System - AutomationEvents](event-system.md#automationeventscs---automation-component-interactions)).

## Dependency Injection

BeaverBuddies uses Timberborn's **Bindito** dependency injection framework. Two configurators register services into different scene contexts:

### ReplayConfigurator (Game Context)

Decorated with `[Context("Game")]`, this configurator runs when a game map loads. It performs two phases of registration:

1. **Always registered** (even in single-player): `ClientConnectionService`, `ClientConnectionUI`, `SteamOverlayConnectionService`, `RegisteredLocalizationService`, `Settings`, and multi-start services.

2. **Co-op only** (guarded by `if (EventIO.IsNull) return`): `ReplayService`, `TickProgressService`, `TickingService`, `DeterminismService`, `TickReplacerService`, `RehostingService`, `ReportingService`, `LateTickableBuffer`, and `DesyncDetecterService`.

The guard on `EventIO.IsNull` is what distinguishes a regular single-player game from a co-op session. `EventIO` is set before the game scene loads -- by the connection flow that establishes server/client roles -- so if it is null, co-op services are skipped entirely.

### ConnectionMenuConfigurator (MainMenu Context)

Decorated with `[Context("MainMenu")]`, this configurator runs when the main menu loads. It registers UI and connection services needed before a game starts: `ClientConnectionService`, `ClientConnectionUI`, `FirstTimerService`, `ConfigIOService`, `MultiplayerMapMetadataService`, `SteamOverlayConnectionService`, and `Settings`.

### Reset on Transition

Both configurators call `SingletonManager.Reset()` as their first action. `ConnectionMenuConfigurator` additionally calls `EventIO.Reset()` to tear down any active network connection when returning to the main menu.

## SingletonManager

`SingletonManager` is a custom singleton registry that exists alongside (and independent of) Timberborn's own DI container. It provides a lightweight way for Harmony patches and static methods to access services without constructor injection.

```csharp
public static class SingletonManager
{
    private static Dictionary<Type, object> map = new Dictionary<Type, object>();

    public static void Reset();                          // Calls IResettableSingleton.Reset() on each, then clears
    public static T RegisterSingleton<T>(T singleton);   // Add to registry
    public static T GetSingleton<T>();                   // Lookup by type, returns default if missing
}
```

Services opt in by extending `RegisteredSingleton`, which auto-registers in its constructor:

```csharp
public class RegisteredSingleton
{
    public RegisteredSingleton()
    {
        SingletonManager.RegisterSingleton(this);
    }
}
```

The `IResettableSingleton` interface allows singletons with static state to clean up when the scene transitions (e.g., returning to the main menu or loading a new map):

```csharp
public interface IResettableSingleton
{
    void Reset();
}
```

This is important because Harmony patches reference static fields that persist across scene loads. Without explicit reset, stale references from a previous game session could cause errors.

## Server vs Client Architecture

BeaverBuddies uses an **event sourcing** pattern to keep multiple game instances in sync. Both the server and all clients run the full Timberborn simulation independently. Rather than transmitting game state, only player actions (as `ReplayEvent` objects) are exchanged. Because the simulation is deterministic, applying the same events in the same order produces identical game states.

### UserEventBehavior

The `UserEventBehavior` enum controls how user-initiated actions are handled depending on the role:

| Value | Used By | Behavior |
|-------|---------|----------|
| `Play` | `FileWriteIO` | Execute the action locally immediately (single-player recording) |
| `Send` | `ClientEventIO` | Do not execute locally; send the event to the server for approval |
| `QueuePlay` | `ServerEventIO` | Queue the event to play on the next tick (ensures ordering consistency) |

### Data Flow

**Server (host):**
1. Player performs an action (e.g., places a building)
2. Harmony prefix intercepts the call, creates a `ReplayEvent`
3. Event is queued (not executed immediately) via `QueuePlay`
4. At the start of the next tick, queued events are played and broadcast to clients
5. Server sends a `HeartbeatEvent` each tick so clients know to advance

**Client (joiner):**
1. Player performs an action
2. Harmony prefix intercepts the call, creates a `ReplayEvent`
3. Event is sent to the server (`Send` behavior); the local action is cancelled (prefix returns `false`)
4. Server receives the event, queues it, and broadcasts it back to all clients on the next tick
5. Client receives the event and replays it locally

### EventIO Interface

The `EventIO` interface abstracts the transport layer:

```csharp
public interface EventIO
{
    void Update();
    List<ReplayEvent> ReadEvents(int ticksSinceLoad);
    void WriteEvents(params ReplayEvent[] events);
    void Close();
    bool RecordReplayedEvents { get; }
    UserEventBehavior UserEventBehavior { get; }
    bool IsOutOfEvents { get; }
    bool ShouldSendHeartbeat { get; }
    bool HasEventsForTick(int tick);
}
```

Implementations:

- **`ServerEventIO`** -- Wraps `TimberServer`. Records replayed events, sends heartbeats, queues user events.
- **`ClientEventIO`** -- Wraps `TimberClient`. Does not re-record received events, sends user events to server.
- **`FileWriteIO`** -- Writes events to a JSON file for replay recording.
- **`FileReadIO`** -- Reads events from a JSON file for replay playback.

The network implementations (`ServerEventIO`, `ClientEventIO`) extend `NetIOBase<T>`, which handles JSON serialization/deserialization of events via `JObject` and the `JsonSettings` serializer.

## Key Design Patterns

### Event Sourcing

All player actions are captured as serializable `ReplayEvent` objects. The game state is a function of the initial save file plus the ordered sequence of events applied to it. This means:
- No game state is transmitted over the network (only events)
- Events can be saved to a file and replayed later
- Desyncs can be detected by comparing state hashes at each tick

### Deterministic Replay

Both server and client run identical simulations. The `DeterminismService` tracks the random number generator state (`randomS0Before` on each event) and other game state hashes to verify that all participants remain in sync. The `Fixes/` directory contains Harmony patches that correct non-deterministic behavior in Timberborn (e.g., hash set iteration order, parallel execution).

### Harmony Method Interception

Nearly every synced player action is captured via Harmony **prefix patches**. The standard pattern is:

```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.TargetMethod))]
class TargetMethodPatcher
{
    static bool Prefix(/* parameters */)
    {
        return ReplayEvent.DoPrefix(() =>
        {
            return new SomeEvent() { /* populate fields */ };
        });
    }
}
```

The prefix returns `false` to cancel the original method when the event should not execute locally (client behavior), or `true` to allow it (server/replay behavior). This is controlled by `EventIO.ShouldPlayPatchedEvents`.

### Transport Abstraction

The `EventIO` interface decouples the event system from any specific transport. The same `ReplayService` works with TCP networking, Steam P2P, or file-based replay without modification. The `NetIOBase<T>` class provides shared serialization logic for the network transports, while `FileWriteIO` and `FileReadIO` handle offline recording and playback.
