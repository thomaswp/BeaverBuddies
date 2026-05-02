# Networking and IO Layer

BeaverBuddies uses a custom networking stack split into two layers: **TimberNet**, a standalone networking library with no Timberborn dependencies, and the **IO bridge layer**, which connects TimberNet to the game's event replay system.

## Architecture Overview

```
+------------------+       +------------------+
|  ServerEventIO   |       |  ClientEventIO   |    IO Bridge Layer
|  (EventIO impl)  |       |  (EventIO impl)  |    (BeaverBuddies)
+--------+---------+       +--------+---------+
         |                          |
+--------+---------+       +--------+---------+
|   TimberServer   |       |   TimberClient   |    TimberNet Library
|  (TimberNetBase) |       |  (TimberNetBase) |    (standalone)
+--------+---------+       +--------+---------+
         |                          |
+--------+---------+       +--------+---------+
| ISocketListener  |       |  ISocketStream   |    Transport Abstraction
| (TCP / Steam /   |       |  (TCP / Steam)   |
|  Multi)          |       |                  |
+------------------+       +------------------+
```

---

## TimberNet Library

TimberNet lives in its own project (`/TimberNet/`) and has zero dependencies on Timberborn or Unity. It depends only on `Newtonsoft.Json` and standard .NET libraries. This separation means it can be tested and developed independently of the game.

**Key source files:**
- `TimberNet/TimberNetBase.cs`
- `TimberNet/TimberServer.cs`
- `TimberNet/TimberClient.cs`

---

## Message Protocol

All messages use a simple framing protocol:

```
+-------------------+-------------------------------+
| 4 bytes (length)  | N bytes (GZip-compressed JSON) |
+-------------------+-------------------------------+
```

1. **Length prefix** -- 4-byte big-endian integer indicating the size of the payload.
2. **Payload** -- GZip-compressed JSON string (UTF-8 encoded before compression).

Every JSON message contains at least two fields:
- `ticksSinceLoad` -- the tick number this event belongs to
- `type` -- the event type identifier

### Constants and Limits

| Constant | Value | Location |
|---|---|---|
| `HEADER_SIZE` | 4 bytes | `TimberNetBase` |
| `MAX_BUFFER_SIZE` | 32,768 bytes (32 KB) | `TimberNetBase` |
| TCP `MaxChunkSize` | 32,768 bytes (32 KB) | `TCPClientWrapper` |
| TCP `MaxBytesPerSecond` | 1,048,576 (1 MB/s) | `TCPClientWrapper` |
| Steam `MaxChunkSize` | 8,192 bytes (8 KB) | `SteamSocket` |
| Steam `MaxBytesPerSecond` | 131,072 (128 KB/s) | `SteamSocket` |

### Chunked Sending

`SendDataWithLength()` splits large payloads into `MaxChunkSize` chunks, sleeping between them to respect `MaxBytesPerSecond`. The sleep duration is calculated as:

```
sleepMS = MaxChunkSize * 1000 / MaxBytesPerSecond
```

For TCP this is effectively zero (32 KB at 1 MB/s = ~31 ms). For Steam it is more significant (8 KB at 128 KB/s = ~62 ms), since Steam's P2P layer can only buffer 1 MB at a time.

---

## TimberNetBase

`TimberNetBase` (`TimberNet/TimberNetBase.cs`) is the abstract base class shared by both server and client. It handles message framing, hash-based state verification, event queuing, and tick counting.

### Message Framing

- **`SendLength(ISocketStream, int)`** -- Writes a 4-byte big-endian length prefix to the stream.
- **`SendDataWithLength(ISocketStream, byte[])`** -- Sends the length prefix followed by the data in chunks, respecting the stream's bandwidth limits.
- **`MessageToBuffer(JObject)`** / **`MessageToBuffer(string)`** -- Serializes a message to a GZip-compressed byte array via `CompressionUtils.Compress()`.
- **`BufferToStringMessage(byte[])`** -- Decompresses a byte array back to a JSON string via `CompressionUtils.Decompress()`.
- **`TryReadLength(ISocketStream, out int)`** -- Reads the 4-byte big-endian length prefix from a stream.

### Hash-Based State Verification

The `Hash` property maintains a running hash of all events processed, used to detect desync between server and client.

- **`Hash`** (property) -- Current hash value, initialized to `17`.
- **`AddToHash(string)`** / **`AddToHash(byte[])`** -- Folds new data into the running hash using `CombineHash`.
- **`CombineHash(int h1, int h2)`** -- `h1 * 31 + h2` (a standard multiplicative hash combiner).
- **`AddEventToHash(JObject)`** -- For `SetState` events, resets the hash to the value in the message. For all other events, hashes the JSON string representation.
- **`AddFileToHash(byte[])`** -- Hashes the raw map bytes (called when the client receives the map file).

### Event Queue and Processing

Events arrive on background threads and are queued for processing on the main thread:

1. **`receivedEventQueue`** (`ConcurrentQueue<string>`) -- Thread-safe queue for raw JSON strings received from the network.
2. **`receivedEvents`** (`List<JObject>`) -- Sorted list of parsed events waiting to be processed, ordered by tick.
3. **`Update()`** -- Drains the concurrent queue into `receivedEvents` (via `ProcessReceivedEventsQueue`), processes pending maps, and fires log events.
4. **`ReadEvents(int ticksSinceLoad)`** -- Sets the current `TickCount`, calls `Update()`, pops events for the current tick, calls `ProcessReceivedEvent` on each, then returns only game-relevant events (filtering out `SetState` and `Heartbeat`).
5. **`InsertInScript(JObject, List<JObject>)`** -- Inserts a message into a sorted list by tick number.
6. **`PopEventsForTick<T>(int, List<T>, Func<T, int>)`** -- Static helper that removes and returns all events from the front of a list whose tick is at or before the given tick.

### Tick Tracking

- **`TickCount`** (property) -- Current tick count, updated each time `ReadEvents` is called.
- **`TicksBehind`** (property) -- How many ticks the last received event is ahead of the current tick. Returns 0 if no events are queued.
- **`ShouldTick`** (virtual property) -- Returns `true` if `Started` is true. Overridden by `TimberClient` to also require events in the queue.

### Listening Loop

`StartListening(ISocketStream, bool isClient)` runs on a background thread and continuously reads messages:

1. Reads the 4-byte length prefix.
2. If this is the first message and we are a client, treats it as the map file (or an error message if length is 0).
3. For subsequent messages, reads the full payload and enqueues the decompressed JSON string into `receivedEventQueue`.

---

## TimberServer

`TimberServer` (`TimberNet/TimberServer.cs`) extends `TimberNetBase` to manage multiple connected clients.

### Construction

```csharp
public TimberServer(
    ISocketListener listener,
    Func<Task<byte[]>> mapProvider,
    Func<JObject>? initEventProvider
)
```

- `listener` -- The socket listener (TCP, Steam, or Multi) that accepts incoming connections.
- `mapProvider` -- Async function that provides the serialized map bytes to send to new clients.
- `initEventProvider` -- Optional function that creates an initialization event (e.g., `InitializeClientEvent` with random seed state) sent to newly connecting clients.

### Client Connection Flow

When `Start()` is called, the server begins accepting clients in a background task loop:

1. `listener.AcceptClient()` blocks until a client connects.
2. If `IsAcceptingClients` is `false`, sends an error message and closes the connection.
3. **`SendMap(client)`** -- Awaits the map bytes from `mapProvider`, calls `StartQueuing(client)` to begin buffering events for this client, then sends the map using `SendDataWithLength`.
4. **`SendState(client)`** -- Sends a `SetState` event with tick 0 and the current `Hash` value, so the client starts with the correct hash. This is sent directly (not queued).
5. If an `initEventProvider` is set, calls `DoUserInitiatedEvent(initEvent, sendNow: true)` to immediately send the init event to all clients.
6. **`FinishQueuing(client)`** -- Flushes the queued messages to the client and removes it from the queuing map, so future events are sent directly.
7. `StartListening(client, false)` -- Enters the infinite read loop for this client.

### Event Handling

- **`ReceiveEvent(JObject)`** -- Overrides the base to stamp the current `TickCount` onto incoming client messages before inserting them into the event queue. This ensures client events are assigned to the server's current tick.
- **`DoUserInitiatedEvent(JObject)`** -- Adds the event to the hash (via base class) and sends it to all connected clients.
- **`SendEventToClients(JObject, bool sendNow)`** -- Iterates over clients, removing disconnected ones. Uses a lock on `queuedMessages` to prevent races during client setup.
- **`QueueOrSentToClient(ISocketStream, JObject)`** -- If the client is still in the queuing phase (map is being sent), buffers the message. Otherwise sends immediately.

### Heartbeats

**`SendHeartbeat()`** creates a `Heartbeat` event with the current tick and processes it as a user-initiated event. This ensures clients receive at least one event per tick so they know to advance.

### Lifecycle

- **`StopAcceptingClients(string errorMessage)`** -- Sets an error message that will be sent to any future connection attempts. Called after the first tick to prevent late joins (since Timberborn randomly initializes some state during load that is not serialized).
- **`Close()`** -- Closes all client connections and stops the listener.

---

## TimberClient

`TimberClient` (`TimberNet/TimberClient.cs`) extends `TimberNetBase` to connect to a server.

### Key Behaviors

- **`ShouldTick`** -- Returns `true` only if `Started` is true AND `receivedEvents.Count > 0`. This means the client will not advance the game until it has events from the server to process.
- **`DoUserInitiatedEvent(JObject)`** -- Does NOT add the event to the local hash. Instead, it sends the event to the server and waits for the server to echo it back with an assigned tick. This ensures server-authoritative ordering.
- **`ProcessReceivedEvent(JObject)`** -- Calls `AddEventToHash()` on the received event, keeping the client's hash in sync with the server's.

### Connection

`Start()` calls `client.ConnectAsync().Wait(3000)` with a 3-second timeout. If the connection times out, throws `ConnectionFailureException`. Then starts a background task running `StartListening(client, true)` to receive the map file and subsequent events.

---

## Transport Abstraction

The networking layer uses two interfaces to abstract the underlying transport:

### ISocketStream

Defined in `TimberNet/ISocketStream.cs`:

```csharp
public interface ISocketStream
{
    bool Connected { get; }
    string? Name { get; }
    int MaxChunkSize { get; }
    int MaxBytesPerSecond { get; }
    int Read(byte[] buffer, int offset, int count);
    void Write(byte[] buffer, int offset, int count);
    void Close();
    Task ConnectAsync();
    byte[] ReadUntilComplete(int count);       // default implementation
    void ReadUntilComplete(byte[] buffer, int count); // default implementation
}
```

`ReadUntilComplete` is a default interface method that loops `Read()` calls until the requested number of bytes have been received, throwing `IOException` if the stream ends prematurely.

### ISocketListener

Defined in `TimberNet/ISocketListener.cs`:

```csharp
public interface ISocketListener
{
    void Start();
    void Stop();
    ISocketStream AcceptClient();  // blocking
}
```

### TCP Transport

**`TCPClientWrapper`** (`TimberNet/TCPClientWrapper.cs`) wraps a standard `TcpClient`:

- `MaxChunkSize` = 32 KB, `MaxBytesPerSecond` = 1 MB/s
- Two constructors: one with address/port for outbound connections, one with an existing `TcpClient` for server-accepted sockets.
- `ConnectAsync()` delegates to `TcpClient.ConnectAsync()`.

**`TCPListenerWrapper`** (`TimberNet/TCPListenerWrapper.cs`) wraps `TcpListener`:

- Binds to `IPAddress.IPv6Any` with `IPv6Only = false`, enabling dual-stack (IPv4 and IPv6) on a single socket.
- `AcceptClient()` blocks and returns a `TCPClientWrapper` wrapping the accepted `TcpClient`.

### Steam Transport

**`SteamSocket`** (`BeaverBuddies/Steam/SteamSocket.cs`) implements `ISocketStream` using Steamworks P2P networking:

- `MaxChunkSize` = 8 KB, `MaxBytesPerSecond` = 128 KB/s (Steam can only buffer 1 MB at a time).
- Uses `SteamNetworking.SendP2PPacket()` with `k_EP2PSendReliable` for writes.
- Reads are backed by a `ConcurrentQueueWithWait<byte[]>` -- the `Read()` method blocks until data is available.
- `ConnectAsync()` is a no-op (returns `Task.CompletedTask`) since the connection is already established when the user joins the Steam lobby.
- Implements `ISteamPacketReceiver` to register with the `SteamPacketListener`.

**`SteamListener`** (`BeaverBuddies/Steam/SteamListener.cs`) implements `ISocketListener` using Steam lobbies:

- On `Start()`, creates a friends-only lobby via `SteamMatchmaking.CreateLobby()` with a capacity of 8.
- Listens for `LobbyChatUpdate_t` callbacks. When a user joins, creates a `SteamSocket` for them and enqueues it.
- `AcceptClient()` blocks on a `ConcurrentQueueWithWait<SteamSocket>` until a user joins the lobby.
- `ShowInviteFriendsPanel()` opens the Steam overlay invite dialog.
- Lobby visibility is controlled by `Settings.LobbyJoinable` (friends-only vs. invite-only).

**`SteamPacketListener`** (`BeaverBuddies/Steam/SteamPacketListener.cs`) polls for incoming Steam packets on the main thread:

- Maintains a `Dictionary<CSteamID, SteamSocket>` of registered sockets.
- `Update()` calls `SteamNetworking.IsP2PPacketAvailable()` in a loop, reading each packet with `ReadP2PPacket()` and routing it to the correct `SteamSocket.ReceiveData()` by `CSteamID`.

### MultiSocketListener

`MultiSocketListener` (`TimberNet/MultiSocketListener.cs`) allows the server to accept connections from multiple transport types simultaneously:

- Takes an array of `ISocketListener` instances (e.g., one TCP, one Steam).
- On first `AcceptClient()` call, starts a background task per listener that continuously accepts clients and enqueues them into a shared `ConcurrentQueueWithWait<ISocketStream>`.
- `AcceptClient()` blocks until any listener produces a connection.
- `GetListener<T>()` retrieves a specific child listener by type.

---

## IO Bridge Layer

The IO bridge layer connects TimberNet to BeaverBuddies' event replay system. It lives in `BeaverBuddies/IO/`.

### EventIO Interface

`EventIO` (`BeaverBuddies/IO/EventIO.cs`) is the central interface that `ReplayService` uses to read and write events:

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
    int TicksBehind { get; }
    bool ShouldSendHeartbeat { get; }
    bool HasEventsForTick(int tick);
}
```

#### UserEventBehavior Enum

Controls what happens when a user-initiated event is recorded:

| Value | Meaning | Used by |
|---|---|---|
| `Play` | Execute the event immediately on the local game | `FileWriteIO` |
| `Send` | Send the event to the server; do not execute locally | `ClientEventIO` |
| `QueuePlay` | Queue the event to be played on the next tick (ensures ordering) | `ServerEventIO` |

#### Static Singleton Pattern

`EventIO` uses a static `instance` field with `Get()`, `Set(EventIO)`, and `Reset()` methods. This allows global access from Harmony patches and other static contexts. `Reset()` calls `Close()` on the current instance before clearing it.

#### Static Helper Properties

- **`ShouldPauseTicking`** -- Returns `true` if the current IO reports `IsOutOfEvents` (used to pause the game when waiting for network data).
- **`ShouldPlayPatchedEvents`** -- Returns `true` if events from Harmony patches should be executed. Always true during replay; otherwise depends on `UserEventBehavior == Play`.
- **`SkipRecording`** -- Returns `true` if currently replaying events and the IO says not to record them (prevents the client from re-sending events it received from the server).

### NetIOBase\<T\>

`NetIOBase<T>` (`BeaverBuddies/IO/NetIOBase.cs`) is the abstract base bridging `TimberNetBase` to `EventIO`:

- Generic over `T : TimberNetBase` (either `TimberServer` or `TimberClient`).
- **`ReadEvents(int)`** -- Delegates to `NetBase.ReadEvents()`, then deserializes each `JObject` into a `ReplayEvent` using `JsonSettings.Deserialize<ReplayEvent>()`.
- **`WriteEvents(ReplayEvent[])`** -- Serializes each `ReplayEvent` to JSON, parses it into a `JObject`, and calls `NetBase.DoUserInitiatedEvent()`.
- **`IsOutOfEvents`** -- Delegates to `!NetBase.ShouldTick`.
- **`TicksBehind`** -- Delegates to `NetBase.TicksBehind`.
- Manages an optional `SteamPacketListener` via `TryRegisterSteamPacketReceiver()`.

### ServerEventIO

`ServerEventIO` (`BeaverBuddies/IO/ServerEventIO.cs`) extends `NetIOBase<TimberServer>`:

| Property | Value | Reason |
|---|---|---|
| `RecordReplayedEvents` | `true` | Server records and sends all events to clients |
| `ShouldSendHeartbeat` | `true` | Clients need heartbeats to know when to advance |
| `UserEventBehavior` | `QueuePlay` | Events are queued until the next tick boundary for deterministic ordering |

**`Start(byte[] mapBytes)`** sets up the server:

1. Creates a `TCPListenerWrapper` on the configured port.
2. If Steam is enabled, also creates a `SteamListener`.
3. Wraps them in a `MultiSocketListener`.
4. Registers any `ISteamPacketReceiver` listeners with a `SteamPacketListener`.
5. Creates a `TimberServer` with a map provider that returns the static `mapBytes` and an init event provider that creates an `InitializeClientEvent`.

**`StopAcceptingClients()`** is called after the first tick to prevent late joins, displaying a user-friendly error message to anyone who tries to connect.

### ClientEventIO

`ClientEventIO` (`BeaverBuddies/IO/ClientEventIO.cs`) extends `NetIOBase<TimberClient>`:

| Property | Value | Reason |
|---|---|---|
| `RecordReplayedEvents` | `false` | Client should not re-send events received from server |
| `ShouldSendHeartbeat` | `false` | Only the server sends heartbeats |
| `UserEventBehavior` | `Send` | Client sends events to the server for validation |

Created via the static factory `ClientEventIO.Create(ISocketStream, MapReceived, Action<string>)`:

1. Registers the socket with a `SteamPacketListener` if applicable.
2. Creates a `TimberClient` and wires up `OnMapReceived`, `OnLog`, and `OnError` callbacks.
3. Calls `NetBase.Start()` to connect.
4. Returns `null` if the connection fails.

---

## File IO

For replay recording and debugging, BeaverBuddies provides file-based `EventIO` implementations in `BeaverBuddies/IO/FileIO.cs`.

### FileWriteIO

Records all events to a JSON file as they happen:

- `UserEventBehavior = Play` -- events execute normally.
- `RecordReplayedEvents = true` -- captures everything.
- `WriteEvents()` serializes each event to JSON and appends it to the file.
- The file format is a JSON array (`[` on open, each event followed by `,`, `]` on close).
- Uses a `ReaderWriterLock` for thread safety.

### FileReadIO

Plays back events from a previously recorded JSON file:

- Reads the entire file on construction, parsing it into a `List<ReplayEvent>`.
- `ReadEvents(int)` uses `TimberNetBase.PopEventsForTick()` to return events up to the requested tick.
- `IsOutOfEvents` returns `true` when the event list is empty (replay is finished).
- `RecordReplayedEvents = false` -- does not re-record during playback.

### RecordToFileService

An `IPostLoadableSingleton` that automatically sets up `FileWriteIO` on game load, saving replays to `Replays/<SaveName>.json`.

---

## CompressionUtils

`CompressionUtils` (`TimberNet/CompressionUtils.cs`) provides static GZip compression and decompression:

```csharp
public static byte[] Compress(string text)    // UTF-8 encode, then GZip at Optimal level
public static string Decompress(byte[] data)  // GZip decompress, then UTF-8 decode
```

Used by `MessageToBuffer` and `BufferToStringMessage` in `TimberNetBase`.

---

## ConcurrentQueueWithWait

`ConcurrentQueueWithWait<T>` (`TimberNet/ConcurrentQueueWithWait.cs`) wraps `ConcurrentQueue<T>` with a `ManualResetEvent` for blocking waits:

- **`Enqueue(T)`** -- Adds an item and signals the event.
- **`WaitAndTryDequeue(out T)`** -- Blocks until an item is available, then dequeues it. Resets the event if the queue is empty after dequeue.
- **`Wait()`** -- Blocks until the event is signaled.

Used by `SteamSocket` (for read buffering), `SteamListener` (for queuing joining users), and `MultiSocketListener` (for aggregating accepted connections).

---

## Data Flow Summary

### Server sending an event

```
ReplayService.RecordEvent()
  -> eventsToPlay queue (QueuePlay behavior)
  -> ReplayService.DoTickIO()
    -> ReplayEvents() dequeues, replays, records s0
    -> SendEvents() groups into GroupedEvent
      -> EventIO.WriteEvents()
        -> NetIOBase.WriteEvents()
          -> TimberServer.DoUserInitiatedEvent()
            -> AddEventToHash()
            -> SendEventToClients()
              -> SendEvent() per client
                -> MessageToBuffer() (JSON -> GZip)
                -> SendDataWithLength() (chunked)
```

### Client receiving and processing an event

```
Background thread: StartListening()
  -> ReadUntilComplete() reads length + payload
  -> BufferToStringMessage() (GZip -> JSON)
  -> receivedEventQueue.Enqueue()

Main thread: EventIO.ReadEvents()
  -> NetIOBase.ReadEvents()
    -> TimberClient.ReadEvents()
      -> Update() drains queue into receivedEvents
      -> PopEventsToProcess() for current tick
      -> ProcessReceivedEvent() -> AddEventToHash()
      -> Filter out SetState/Heartbeat
    -> Deserialize JObject to ReplayEvent
  -> ReplayService.ReplayEvents() executes each event
```
