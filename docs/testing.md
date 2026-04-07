# Testing and Debugging

This page covers the testing tools, debugging workflows, and logging infrastructure available in BeaverBuddies.

## ClientServerSimulator

**File:** `ClientServerSimulator/Drivers.cs`

The `ClientServerSimulator` project is a standalone Windows Forms application for testing the networking layer without running Timberborn. It simulates a server and client communicating over localhost.

### DriverBase\<T\>

The abstract `DriverBase<T>` class provides the foundation for both server and client test drivers:

```csharp
internal abstract class DriverBase<T> where T : TimberNetBase
{
    public const string LOCALHOST = "127.0.0.1";
    public const int PORT = 25565;

    protected List<JObject> scriptedEvents;
    public readonly T netBase;
    protected int ticks = 0;

    public DriverBase(string scriptPath, T netBase) { ... }
}
```

**Key methods:**

- **Constructor** -- Takes a path to a JSON script file and a `TimberNetBase` instance. Reads the script file with `ReadScriptFile()`, which parses a JSON array of event objects.
- **`TryTick()`** -- If `netBase.ShouldTick` is true, increments the tick counter and reads events from the network.
- **`Update()`** -- Reads network events, then iterates through scripted events. Uses `TimberNetBase.PopEventsForTick()` to find events scheduled for the current tick and feeds them to `netBase.DoUserInitiatedEvent()`.
- **`ProcessEvent(JObject)`** -- Virtual method for handling received events. The base implementation handles `SpeedSetEvent` by raising the `OnTargetSpeedChanged` event.

### ClientDriver

```csharp
internal class ClientDriver : DriverBase<TimberClient>
{
    const string SCRIPT_PATH = "client.json";

    public ClientDriver() : base(SCRIPT_PATH,
        new TimberClient(new TCPClientWrapper(LOCALHOST, PORT))) { }
}
```

Reads scripted client events from `client.json` and connects to localhost on port 25565.

### ServerDriver

```csharp
internal class ServerDriver : DriverBase<TimberServer>
{
    const string SCRIPT_PATH = "server.json";
    const string SAVE_PATH = "save.timber";

    public ServerDriver() : base(SCRIPT_PATH,
        new TimberServer(new TCPListenerWrapper(PORT),
            () => File.ReadAllBytesAsync(SAVE_PATH),
            CreateInitEvent())) { }
}
```

- Reads the save file from `save.timber` to provide map bytes to connecting clients.
- Creates a `RandomStateSetEvent` as the init event with a fixed seed (`1234`) for deterministic testing.
- Overrides `ProcessEvent` to also call `DoUserInitiatedEvent` on the server (simulating the server replaying its own events).
- Overrides `Update` to wait for at least one client before processing scripted events.

### Writing Test Scripts

Test scripts are JSON arrays where each element is an event object with a `ticksSinceLoad` field indicating when it should fire:

```json
[
  { "$type": "BeaverBuddies.Events.SpeedSetEvent", "ticksSinceLoad": 0, "speed": 1 },
  { "$type": "BeaverBuddies.Events.PlaceBuildingEvent", "ticksSinceLoad": 10, ... }
]
```

## Inspector Project

**Directory:** `Inspector/`

The `Inspector` project is a console application containing utilities and tests.

### UpdateLocalizations

**File:** `Inspector/UpdateLocalizations.cs`

A utility for batch-updating localization CSV files. It:

1. Reads structured translation input from `input.txt` (formatted as markdown with locale headers and CSV code blocks).
2. Parses blocks matching the pattern `### ... (deDE) \`\`\`csv ... \`\`\``.
3. For each locale, finds the matching CSV file in `BeaverBuddies/Localizations/`.
4. Adds new keys that are not already present, skipping duplicates.
5. Reports a summary of added keys, missing CSV files, and unmatched locales.

### Unit Tests

The Inspector project also contains unit tests for networking components:

- **`ConcurrentQueueWithWaitTests.cs`** -- Tests for the thread-safe queue used in Steam networking.
- **`HasSetTest.cs`** -- Tests validating HashSet enumeration order determinism (relevant to the desync investigation documented in `DeterminismService.cs`).

## Replay File Debugging

**File:** `BeaverBuddies/IO/FileIO.cs`

BeaverBuddies includes a file-based event recording and playback system for reproducing bugs offline.

### FileWriteIO

`FileWriteIO` implements the `EventIO` interface and writes all events to a JSON file as they occur:

```csharp
public class FileWriteIO : EventIO
{
    public bool RecordReplayedEvents => true;
    public UserEventBehavior UserEventBehavior => UserEventBehavior.Play;

    public void WriteEvents(params ReplayEvent[] events)
    {
        for (int i = 0; i < events.Length; i++)
        {
            string json = JsonConvert.SerializeObject(e, settings);
            WriteToFile(json + ",");
        }
    }
}
```

The output file is a JSON array written incrementally (opened with `[`, each event appended with a trailing comma, closed with `]`). Thread safety is maintained via a `ReaderWriterLock`.

`RecordToFileService` is an `IPostLoadableSingleton` that automatically sets up `FileWriteIO` when enabled. The file is saved to `Replays/<saveName>.json`.

### FileReadIO

`FileReadIO` reads a previously recorded JSON file and replays events at the correct ticks:

```csharp
public List<ReplayEvent> ReadEvents(int ticksSinceLoad)
{
    return TimberNetBase.PopEventsForTick(ticksSinceLoad, events, e => e.ticksSinceLoad);
}
```

It loads the entire file at construction time and uses `PopEventsForTick` to return events matching the current tick. `IsOutOfEvents` returns true when all events have been consumed.

### Usage

To record a session:
1. Enable `RecordToFileService` (or set up `FileWriteIO` manually).
2. Play through the scenario you want to capture.
3. The JSON file is written to the `Replays/` directory.

To replay a session:
1. Create a `FileReadIO` pointing to the recorded JSON file.
2. Set it as the active `EventIO` via `EventIO.Set()`.
3. Load the same save file. Events replay automatically at the correct ticks.

This is particularly useful for reproducing desyncs -- record the server's events, then replay them on a client to see where divergence occurs.

## Desync Debugging Workflow

When a desync occurs, follow this workflow to identify the root cause.

### Step 1: Enable Tracing

Set `Settings.AlwaysTrace` to `true` in the mod settings, or use the one-time flag:

```csharp
Settings.TemporarilyDebug = true;
```

`TemporarilyDebug` enables debug mode for the current session without persisting the setting. It is useful when you want to reproduce a specific desync once.

When `Settings.Debug` is true:

- `DesyncDetecterService` records per-tick traces on both server and client.
- Traces are exchanged via `TraceLoggedForTickEvent` events.
- The RNG state (`s0`) is logged at each random call during ticks.
- Additional trace points fire in natural resource reproduction, pathfinding, water simulation, and entity bucket management.

### Step 2: Reproduce the Desync

Play the game until the desync occurs. With tracing enabled, both sides record detailed logs of what happened each tick.

### Step 3: Compare Traces

When a desync is detected, `DesyncDetecterService.VerifyTraces()` automatically generates a detailed comparison log:

```
Desync detected for tick 42!
========== Trace history ==========
(shared history from previous ticks)
========== Shared History for Desynced Tick 42 ==========
Tick 42 started
Marking spots for Tree at (5, 0, 3) (guid-1234)
Spots updated: 0 --> 2
========== Desynced Trace ==========
---------- My Trace ----------
Spawning: Tree, (6, 0, 4)
---------- Other Trace ----------
Spawning: Tree, (7, 0, 5)
========== Desynced Log End ==========
```

The log shows:
- **Shared history** -- What both sides agreed on before divergence.
- **Desynced trace** -- The first point where the traces differ, with stack traces for the last few entries.

### Step 4: Identify the Divergence

Common patterns to look for:

- **RNG state mismatch before a specific trace** -- Something consumed random numbers non-deterministically between two trace points.
- **Missing or extra trace entries** -- A system executed on one side but not the other.
- **Different values in hash traces** -- Water or moisture maps diverged, indicating a parallel tick ordering issue.

### Step 5: Add More Trace Points

If the existing traces are insufficient, add new `DesyncDetecterService.Trace()` calls in the suspected area. Always guard with `if (!Settings.Debug) return;` to avoid performance overhead in normal play:

```csharp
if (!Settings.Debug) return;
DesyncDetecterService.Trace($"MySystem processing {entityId} at {coordinates}");
```

## ReportingService

**File:** `BeaverBuddies/Reporting/ReportingService.cs`

`ReportingService` handles automated desync report submission to an Airtable database, subject to user consent (`Settings.ReportingConsent`).

### PostDesync

The main method `PostDesync()` submits:

| Field | Content |
|---|---|
| `SaveID` | Hash of the map name |
| `EventID` | Hash of the desync trace (from `DesyncDetecterService.GetLastDesyncID()`) |
| `Role` | "Server" or "Client" |
| `IsCrash` | Always `false` for desyncs |
| `DesyncTrace` | The desync trace text (compressed if too large) |
| `Logs` | The Unity console log (read from `Application.consoleLogPath`) |
| `VersionInfo` | Mod and game version information |

If the trace exceeds Airtable's 100,000 character limit, it is compressed to Base64 first. If still too large, it is truncated.

Map save data can optionally be uploaded as a zip attachment to the created record.

### Authentication

The Airtable access token is embedded as a resource (`pat.properties`) at build time. If the file is not present, reporting is silently disabled.

## Logging

**File:** `BeaverBuddies/Plugin.cs`

BeaverBuddies uses BepInEx's logging infrastructure through the `Plugin` class:

### Log Methods

```csharp
public static void Log(string message)       // Info level, respects SilenceLogging
public static void LogWarning(string message) // Warning level, always logged
public static void LogError(string message)   // Error level, always logged
public static void LogStackTrace()            // Dumps current stack trace at info level
```

All messages are prefixed with a timestamp (`[HH-mm-ss.ff]`).

`Plugin.Log()` respects `Settings.VerboseLogging` -- when `SilenceLogging` is true, info-level messages are suppressed. Warnings and errors are always logged regardless of this setting.

### BepInEx Console

BepInEx provides a console window (on Windows) or log file that captures all output. The log file path is available via `UnityEngine.Application.consoleLogPath` and is used by `ReportingService` when submitting desync reports.

### Conditional Logging in Debug Mode

Many trace and diagnostic messages are guarded by `Settings.Debug`:

```csharp
if (Settings.Debug)
{
    DesyncDetecterService.Trace($"Tick RNG; s0 before: {UnityEngine.Random.state.s0:X8}");
}
```

This two-tier approach (warning messages always visible, detailed traces only in debug mode) keeps normal gameplay performant while providing deep diagnostics when needed.
