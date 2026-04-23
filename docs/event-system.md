# Event System

This page provides a deep dive into BeaverBuddies' event system -- the mechanism by which all player actions are captured, serialized, transmitted, and replayed across multiplayer clients.

## Core Concept

Every player action that affects game state is intercepted by a Harmony prefix patch and converted into a `ReplayEvent` object. These events are serialized to JSON, sent over the network (or written to a file), and replayed on all participants. Because the Timberborn simulation is deterministic, applying the same sequence of events to the same starting save produces identical game states on all clients.

The event lifecycle:

1. Player performs an action (e.g., places a building)
2. Harmony prefix intercepts the game method call
3. A `ReplayEvent` is created with the relevant parameters
4. The event is passed to `ReplayService.RecordEvent()`
5. Depending on `UserEventBehavior`, the original method either executes or is cancelled
6. Events are transmitted via `EventIO` to all participants
7. On the receiving side, `ReplayEvent.Replay()` is called to execute the action

## ReplayEvent Base Class

**File:** `BeaverBuddies/Events/ReplayEvent.cs`

All events inherit from the abstract `ReplayEvent` class:

```csharp
public abstract class ReplayEvent : IComparable<ReplayEvent>
{
    public int ticksSinceLoad;      // The tick number when this event occurred
    public int? randomS0Before;     // RNG state before the event (for desync detection)
    public string type => GetType().Name;  // Serialized type name for debugging

    public abstract void Replay(IReplayContext context);
}
```

### Key Fields

| Field | Type | Purpose |
|-------|------|---------|
| `ticksSinceLoad` | `int` | Tick counter at event creation. Used to order events and ensure they replay at the correct tick. |
| `randomS0Before` | `int?` | Snapshot of the random number generator seed before the event. Used by `DeterminismService` to detect desyncs. |
| `type` | `string` | The class name of the event (read-only property). Included in JSON for debugging. |

### IReplayContext

Events access game services through `IReplayContext`, which provides a `GetSingleton<T>()` method. This is implemented by `ReplayService` and allows events to look up any registered Timberborn service (e.g., `EntityRegistry`, `BlockObjectPlacerService`, `SpeedManager`).

### Helper Methods

`ReplayEvent` provides static helpers used throughout the event classes:

- **`GetEntityComponent(context, entityID)`** -- Looks up an entity by its GUID string via `EntityRegistry`
- **`GetComponent<T>(context, entityID)`** -- Gets a typed component from an entity
- **`GetEntityID(component)`** -- Extracts the GUID string from a `BaseComponent`
- **`GetBuilding(context, buildingName)`** -- Looks up a `BuildingSpec` by template name

### The DoPrefix Flow

The `DoPrefix` static method is the standard pattern used by all Harmony prefix patches to intercept game methods:

```csharp
public static bool DoPrefix(Func<ReplayEvent> getEvent)
{
    // 1. If already replaying events, let the original method run
    if (ReplayService.IsReplayingEvents) return true;

    // 2. If ReplayService is unavailable (no co-op), use default behavior
    ReplayService replayService = GetReplayServiceIfReady();
    if (replayService == null) return true;

    // 3. Create the event; if null, use default behavior
    ReplayEvent message = getEvent();
    if (message == null) return true;

    // 4. Record the event with ReplayService
    replayService.RecordEvent(message);

    // 5. Return based on whether this side should execute the action
    return EventIO.ShouldPlayPatchedEvents;
}
```

The return value controls whether the original game method executes:
- **Server (`QueuePlay`)**: Returns `false` -- the action is queued and will execute on the next tick during replay
- **Client (`Send`)**: Returns `false` -- the action is sent to the server; it will execute when received back
- **Replay**: Returns `true` -- during `ReplayService`'s event replay loop, `IsReplayingEvents` is true, so the original method runs normally

### DoEntityPrefix

A convenience wrapper for entity-scoped events:

```csharp
public static bool DoEntityPrefix(BaseComponent component, Func<string, ReplayEvent> doRecord)
```

This extracts the entity GUID from the component and passes it to the event creation function. If the component has no `EntityComponent` (e.g., it is a prefab), it returns `true` to let the default behavior run without recording.

## Event Categories

### ToolEvents.cs -- Building, Demolition, Planting, Zoning

**File:** `BeaverBuddies/Events/ToolEvents.cs`

This is the largest event file, covering all spatial tool interactions:

| Event Class | Patched Method | Description |
|-------------|---------------|-------------|
| `BuildingPlacedEvent` | `BuildingPlacer.Place` | Places a building at specific coordinates with orientation, flip mode, and optional duplication source |
| `BuildingsDeconstructedEvent` | `BlockObjectDeletionTool.DeleteBlockObjects` | Deletes one or more buildings by entity ID |
| `PlantingAreaMarkedEvent` | `PlantingSelectionService.MarkArea` / `UnmarkArea` | Marks or unmarks an area for planting a specific tree/crop |
| `ClearResourcesMarkedEvent` | `DemolishableSelectionTool.ActionCallback` | Marks or unmarks resources (trees, ruins) for demolition |
| `TreeCuttingAreaEvent` | `TreeCuttingArea.AddCoordinates` / `RemoveCoordinates` | Adds or removes coordinates from tree-cutting zones |
| `BuildingUnlockedEvent` | `BuildingUnlockingService.Unlock` | Unlocks a building via the science system, including tool button unlocking |

**BuildingPlacedEvent** is notable for its complexity. It stores:
- `prefabName` -- the building template name
- `coordinates` -- `Vector3Int` grid position
- `orientation` -- rotation enum
- `isFlipped` -- flip state
- `duplicationSourceID` -- if the building was placed by duplicating another, this references the source entity so settings can be copied via `Duplicator.Duplicate()`

On replay, it validates the placement by instantiating a temporary preview object and checking `BlockObject.IsValid()` before actually placing.

### TimeEvents.cs -- Speed Control

**File:** `BeaverBuddies/Events/TimeEvents.cs`

| Event Class | Patched Method | Description |
|-------------|---------------|-------------|
| `SpeedSetEvent` | `SpeedManager.ChangeSpeed` | Sets game speed to a specific value |
| `ShowOptionsMenuEvent` | `GameOptionsBox.Show` | Pauses game and shows options (extends `SpeedSetEvent` with speed=0) |

The `SpeedChangePatcher` patches `SpeedManager.ChangeSpeed` directly (rather than using `DoPrefix`) because it needs special handling:
- It skips recording if the speed is already at the target value
- It has a `silently` flag for internal speed changes that should not generate events
- If the client is out of events (`ShouldPauseTicking`), it overrides the requested speed to 0

Additional patchers (`SpeedLockPatcher`, `SpeedUnlockPatcher`) handle dialog-triggered speed locks. Clients skip these entirely since only the host should freeze for dialogs.

### SystemEvents.cs -- Autosave

**File:** `BeaverBuddies/Events/SystemEvents.cs`

| Event Class | Description |
|-------------|-------------|
| `AutosaveEvent` | Defers autosaves to ensure they happen at consistent tick boundaries |

The autosave patcher is currently commented out in the source, but the event class remains. When active, it ensures that autosaves triggered by the game's timer are synchronized -- the client should never autosave independently, and the server defers non-instant saves through the event system.

### ConnectionEvents.cs -- Initialization and Desync Handling

**File:** `BeaverBuddies/Events/ConnectionEvents.cs`

| Event Class | Description |
|-------------|-------------|
| `InitializeClientEvent` | Sent by the server on connection to validate version compatibility |
| `ClientDesyncedEvent` | Triggered when a desync is detected; handles UI, reporting, and recovery |

**InitializeClientEvent** carries three fields for validation:
- `serverModVersion` -- compared against the client's `Plugin.Version`
- `serverGameVersion` -- compared against `GameVersions.CurrentVersion`
- `isDebugMode` -- compared against `Settings.Debug`

If any mismatch is found, a warning dialog is shown to the player.

**ClientDesyncedEvent** is one of the most complex events. On replay it:
1. Pauses the game (`SetTargetSpeed(0)`)
2. Shows a dialog with up to three options:
   - **Report bug** -- if debug tracing is enabled, uploads the desync trace and save file via `ReportingService`; if not, offers to enable tracing
   - **Reconnect/Rehost** -- the host can save and rehost; the client can attempt to reconnect
   - **Cancel** -- dismiss the dialog
3. Handles user consent for data reporting via `Settings.ReportingConsent`

### EntityUIEvents.cs -- Entity Panel Interactions

**File:** `BeaverBuddies/Events/EntityUIEvents.cs`

This file handles all UI interactions on individual entities (buildings, characters):

| Event Class | Patched Method | Description |
|-------------|---------------|-------------|
| `GatheringPrioritizedEvent` | `GatherablePrioritizer.PrioritizeGatherable` | Sets gathering priority for a building |
| `ManufactoryRecipeSelectedEvent` | `Manufactory.SetRecipe` | Selects a recipe for a workshop |
| `BuildingDropdownEvent<T>` | (abstract base) | Generic base for dropdown-style selections on buildings |

These events follow a common pattern using the abstract `BuildingDropdownEvent<Selector>` base class:

```csharp
abstract class BuildingDropdownEvent<Selector> : ReplayEvent where Selector : BaseComponent
{
    public string itemID;    // The selected item (recipe name, resource name, etc.)
    public string entityID;  // The target building's GUID

    public override void Replay(IReplayContext context)
    {
        var selector = GetComponent<Selector>(context, entityID);
        if (selector == null) return;
        SetValue(context, selector, itemID);
    }

    protected abstract void SetValue(IReplayContext context, Selector selector, string id);
}
```

The file also includes numerous other entity interaction events for work scheduling, priority setting, naming, emptying, inventory management, and more. Each follows the standard `DoEntityPrefix` pattern.

### AutomationEvents.cs -- Automation Component Interactions

**File:** `BeaverBuddies/Events/AutomationEvents.cs`

The automation system uses a **reflection-based approach** rather than individual event classes per action. A single `AutomationEvent` class handles all automation component interactions:

```csharp
public class AutomationEvent : ReplayEvent
{
    public string entityID;      // Target entity GUID
    public string methodKey;     // "FullClassName.MethodName" lookup key
    public object[] arguments;   // Serialized method arguments
}
```

**How it works:**

1. `ApplyAutomationPatches(Harmony)` is called at startup with a list of `(Type, string)` tuples specifying which methods to patch
2. For each method, it registers a `UniversalPrefix` as the Harmony prefix and caches the `MethodInfo` in a dictionary keyed by `"ClassName.MethodName"`
3. When any patched method is called, `UniversalPrefix` creates an `AutomationEvent` with the method key and serialized arguments
4. On replay, `Replay()` looks up the `MethodInfo` from the cache, resolves the target component via reflection (`GetComponent` with a runtime type), deserializes the arguments, and invokes the method

The list of patched methods covers the full automation system:

- **Chronometer** -- `SetStartTime`, `SetEndTime`, `SetMode`
- **Sensors** -- `ContaminationSensor`, `DepthSensor`, `FlowSensor` (mode and threshold setters)
- **FireworkLauncher** -- `SetContinuous`, `SetFireworkId`, `SetFlightDistance`, `SetHeading`, `SetPitch`
- **Logic components** -- `Gate.SetOpeningMode`, `Indicator` settings, `Lever` (pin, spring return, switch), `Memory` (mode, inputs), `Relay` (inputs, mode)
- **Counters** -- `PopulationCounter`, `PowerMeter`, `ResourceCounter`, `ScienceCounter` (various setters)
- **Other** -- `Speaker`, `Timer`, `WeatherStation`

The arguments go through a `Serialize`/`Deserialize` step to handle types that need special conversion (e.g., entity references are converted to GUID strings).

### BatchEvents.cs -- District Migration and Population

**File:** `BeaverBuddies/Events/BatchEvents.cs`

| Event Class | Patched Method | Description |
|-------------|---------------|-------------|
| `ManualMigrationEvent` | `ManualMigrationPopulationRow.MigratePopulation` | Moves beavers/bots between districts |
| `SetDistrictMinimumPopulationEvent` | `PopulationDistributor.SetMinimumAndMigrate` | Sets minimum population for a district |
| `SetDistrictMigrationToggledEvent` | `PopulationDistributor.ToggleAllowImmigrationAndMigrate` / `ToggleAllowEmigrationAndMigrate` | Toggles immigration/emigration for a district |

These events use a `DistributorType` enum to identify the population category:

```csharp
enum DistributorType
{
    Children,
    Adults,
    Bots,
    Contaminated,
    Unknown
}
```

The `DistributorUtils` helper class maps between `IDistributorTemplate` instances and the enum, and retrieves the appropriate `PopulationDistributor` from a `DistrictCenter`.

## Special Events

### GroupedEvent

**File:** `BeaverBuddies/ReplayService.cs`

```csharp
class GroupedEvent : ReplayEvent
{
    public List<ReplayEvent> events;
}
```

A `GroupedEvent` wraps multiple events that belong to the same tick. This ensures that when events are transmitted over the network, all events for a given tick arrive together. The receiving side unpacks the group and replays each event individually. `GroupedEvent.Replay()` throws `NotImplementedException` because it should never be replayed directly -- the `ReplayService` handles unpacking.

### HeartbeatEvent

**File:** `BeaverBuddies/ReplayService.cs`

```csharp
class HeartbeatEvent : ReplayEvent
{
    public override void Replay(IReplayContext context)
    {
        // No op
    }
}
```

The server sends a `HeartbeatEvent` at the end of each tick. This signals to clients that the server has completed that tick and they may advance. Without a heartbeat, clients pause and wait -- this is what keeps server and clients in lockstep. The heartbeat is a no-op when replayed; its presence in the event stream is what matters.

## JSON Serialization

**File:** `BeaverBuddies/IO/FileIO.cs`

All events are serialized using Newtonsoft.Json with custom settings defined in the `JsonSettings` class:

```csharp
class JsonSettings : JsonSerializerSettings
{
    public JsonSettings()
    {
        Formatting = Formatting.Indented;
        TypeNameHandling = TypeNameHandling.All;
        Converters.Add(new Vector3Converter());
        Converters.Add(new Vector3IntConverter());
    }
}
```

### Key Configuration

| Setting | Value | Purpose |
|---------|-------|---------|
| `TypeNameHandling` | `TypeNameHandling.All` | Embeds the full .NET type name (`$type`) in every JSON object. This enables polymorphic deserialization -- the deserializer can reconstruct the correct `ReplayEvent` subclass from the JSON without knowing the type in advance. |
| `Formatting` | `Formatting.Indented` | Human-readable output for debugging. |

### Custom Converters

Unity's `Vector3` and `Vector3Int` types do not serialize cleanly with default JSON settings, so custom converters are provided:

- **`Vector3Converter`** -- Serializes `Vector3` as a JSON array `[x, y, z]` of floats
- **`Vector3IntConverter`** -- Serializes `Vector3Int` as a JSON array `[x, y, z]` of integers

### Serialization in Network Transport

In `NetIOBase<T>`, events are serialized to `JObject` for transmission over TimberNet:

```csharp
private static ReplayEvent ToEvent(JObject obj)
{
    return JsonSettings.Deserialize<ReplayEvent>(obj.ToString());
}
```

The `WriteEvents` method serializes events to JSON and passes them to TimberNet, which handles GZip compression at the transport layer. The `ReadEvents` method deserializes received `JObject` instances back into typed `ReplayEvent` subclasses using the `$type` metadata from `TypeNameHandling.All`.

### File-Based Serialization

`FileWriteIO` writes events as a JSON array to disk for replay recording. `FileReadIO` reads the array back and uses `TimberNetBase.PopEventsForTick()` to return events matching the current tick during playback.
