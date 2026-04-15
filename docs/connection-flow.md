# Connection and Session Management

This page documents how BeaverBuddies establishes multiplayer sessions, from the UI entry points through network connection to game loading.

## Hosting a Game

### UI Entry Point

The hosting flow begins in the load game screen. `LoadGameBoxGetPanelPatcher` (in `BeaverBuddies/Connect/ServerHostingUtils.cs`) patches `LoadGameBox.GetPanel` to add a **"Host Co-op Game"** button next to the standard Load button:

```csharp
ButtonInserter.DuplicateOrGetButton(__result, "LoadButton", "HostButton", (button) =>
{
    button.text = _loc.T("BeaverBuddies.Saving.HostCoopGame");
    button.clicked += () => HostSelectedGame(__instance);
});
```

When clicked, `HostSelectedGame` validates the save and calls `ServerHostingUtils.LoadIfSaveValidAndHost()`.

### LoadAndHost Flow

`ServerHostingUtils.LoadAndHost()` orchestrates the full hosting sequence:

1. **Read map bytes** -- `GetMapBtyes()` reads the save file into a byte array via `GameSaveRepository.OpenSaveWithoutLogging()`.

2. **Create ServerEventIO** -- A new `ServerEventIO` is created and set as the active `EventIO`. This starts the TCP server (and optionally a Steam listener) and begins accepting connections.

3. **Show waiting dialog** -- A dialog box displays connected clients in real time. A coroutine (`UpdateDialogBox`) polls `io.NetBase.GetConnectedClients()` each frame and updates the dialog label. If Steam is available, an "Invite Friends" button appears that calls `SteamListener.ShowInviteFriendsPanel()`.

4. **Start Game button** -- When the host clicks "Start Game":
   - The coroutine is stopped.
   - `DeterminismService.InitGameStartState(data)` seeds the RNG from the map hash.
   - `GameSceneLoader.StartSaveGame(saveReference)` loads the save.

5. **Stop accepting clients** -- After the first tick, the server stops accepting new connections (`StopAcceptingClients`).

### Save Validation

`LoadIfSaveValidAndHost` replicates the `ValidatingGameLoader.CheckNextValidator` pattern, iterating through all `IGameLoadValidator` instances before proceeding to `LoadAndHost`. This ensures modded or corrupted saves are caught before hosting.

## Joining a Game

### Direct IP Connection

**File:** `BeaverBuddies/Connect/ClientConnectionService.cs`

`ClientConnectionService.TryToConnect(string address)` handles direct IP connections:

1. **Parse address and port** -- `TryParseHostAndPort()` uses a dummy URI scheme (`tcp://`) to parse the input. It supports IPv4, IPv6 (bracketed), and hostnames. Port defaults to `Settings.Port` (25565) if not specified.

2. **Resolve hostname** -- If the address is not a valid IP, `ResolveHostnameIfNecessary()` calls `Dns.GetHostEntry()` and uses the first result.

3. **Create TCP connection** -- A `TCPClientWrapper` is created with the resolved address and port.

4. **Create ClientEventIO** -- `ClientEventIO.Create(socket, LoadMap, onError)` wraps the socket in a `TimberClient`, connects to the server, and waits for the map data.

5. **Load the map** -- The `LoadMap` callback:
   - Calls `SingletonManager.Reset()` to clean up existing state.
   - Saves the received map bytes to disk under the "Online Games" directory with a hash-based name.
   - Calls `DeterminismService.InitGameStartState(mapBytes)` to seed the RNG identically to the server.
   - Calls `GameSceneLoader.StartSaveGame()` to load the save.

### Steam Connection

`ClientConnectionService.TryToConnect(CSteamID friendID)` creates a `SteamSocket` directly from a Steam friend's ID, then follows the same `ClientEventIO.Create` flow.

### Error Handling

Connection failures show a localized dialog with the specific error reason (invalid format, invalid address, connection failed) and a button linking to the troubleshooting URL.

## Steam Integration

### SteamOverlayConnectionService

**File:** `BeaverBuddies/Steam/SteamOverlayConnectionService.cs`

This service manages Steam overlay integration for joining games. It is conditionally compiled with `#if IS_STEAM`.

On initialization (when `SteamManager.Initialized` becomes true):

- Registers Steamworks callbacks:
  - `GameLobbyJoinRequested_t` -- When a player accepts a Steam invite, joins the lobby.
  - `LobbyEnter_t` -- After entering a lobby, if the current player is not the owner, connects to the lobby owner via `ClientConnectionService.TryToConnect(owner)`.
  - `P2PSessionRequest_t` -- Accepts incoming P2P session requests.

The service also handles the case where the Steam overlay is still open when the connection completes, deferring the success/failure dialog until the overlay closes (via `PanelHiddenEvent`).

### SteamListener

**File:** `BeaverBuddies/Steam/SteamListener.cs`

`SteamListener` implements `ISocketListener` for the server side:

- `Start()` creates a Steam lobby. The lobby type is determined by `Settings.LobbyJoinable`:
  - `true` -> `ELobbyType.k_ELobbyTypeFriendsOnly` (friends can see and join)
  - `false` -> `ELobbyType.k_ELobbyTypeInvisible` (invite-only)
- `OnLobbyChatUpdate` detects when a user joins the lobby, creates a `SteamSocket` for them, registers it with the `SteamPacketListener`, and enqueues it for acceptance.
- `AcceptClient()` blocks until a new client is available in the `joiningUsers` queue.
- `ShowInviteFriendsPanel()` opens the Steam overlay invite dialog via `SteamFriends.ActivateGameOverlayInviteDialog`.
- `Stop()` leaves the lobby and disposes all callbacks.

### SteamSocket

`SteamSocket` (in `BeaverBuddies/Steam/SteamSocket.cs`) implements `ISocketStream` over Steam P2P networking. It wraps `SteamNetworking` API calls for sending and receiving packets.

### SteamOverlayInputBlockerPatch

`SteamOverlayInputBlockerPatch` (in `BeaverBuddies/Steam/SteamOverlayInputBlockerPatch.cs`) prevents game input from being processed while the Steam overlay is open. This is only compiled in Steam builds (`IS_STEAM`).

## Rehosting

**File:** `BeaverBuddies/Connect/RehostingService.cs`

`RehostingService` allows the server to restart a session after a desync or other issue without returning to the main menu.

### RehostGame Flow

1. `RehostGame()` calls `SaveRehostFile()` with a callback to reload.
2. `SaveRehostFile()`:
   - Creates a timestamped save reference (e.g., "2026-04-06 12-00-00 Rehost").
   - Calls `GameSaver.SaveInstantlySkippingNameValidation()` to write the save.
   - When `waitUntilAccessible` is true, the callback is deferred by one frame (via `TimeoutUtils.RunAfterFrames`) to ensure the save stream is released.
3. The callback calls `ServerHostingUtils.LoadIfSaveValidAndHost()`, which starts the full hosting flow with the new save.

This effectively creates a new session from the current game state, allowing disconnected clients to rejoin.

## UI Components

### ClientConnectionUI

**File:** `BeaverBuddies/Connect/ClientConnectionUI.cs`

Adds a **"Join Co-op Game"** button to both the main menu (`MainMenuGetPanelPatcher`) and the in-game options menu (`GameOptionsBoxGetPanelPatcher`). Uses `ButtonInserter.DuplicateOrGetButton` to clone an existing button's style.

When clicked, shows an input dialog (via `InputBoxShower`) pre-filled with the last-used address from `Settings.ClientConnectionAddress`. The input supports up to 128 characters (overriding the default) to accommodate IPv6 addresses and hostnames.

### ButtonInserter

**File:** `BeaverBuddies/Connect/ButtonInserter.cs`

Utility class that duplicates an existing UI button by name, giving it a new name and configuration callback. This ensures buttons match the game's visual style without custom UXML.

### FirstTimerService

**File:** `BeaverBuddies/Connect/FirstTimerService.cs`

An `IPostLoadableSingleton` that shows a welcome dialog the first time a player loads the game with BeaverBuddies installed. The dialog provides a link to the guide (via `LinkHelper.GuideURL`). After the player clicks either the guide link or cancel, `Settings.ShowFirstTimerMessage` is set to `false` so the dialog does not appear again.

## Connection Sequence Diagram

```
Server                                Client
  |                                      |
  |  LoadAndHost()                       |
  |  - Read save bytes                   |
  |  - Create ServerEventIO             |
  |  - Start TCP/Steam listener          |
  |  - Show waiting dialog               |
  |                                      |
  |  <---- TCP/Steam connect ----------  |  TryToConnect()
  |  ---- Send map bytes ------------->  |
  |                                      |  LoadMap()
  |  Host clicks "Start Game"            |  - Reset singletons
  |  - InitGameStartState(bytes)         |  - Save map to disk
  |  - StartSaveGame()                   |  - InitGameStartState(bytes)
  |                                      |  - StartSaveGame()
  |                                      |
  |  [Both load with identical           |
  |   RNG seed and save data]            |
  |                                      |
  |  First tick completes                |
  |  - StopAcceptingClients()            |
  |                                      |
```
