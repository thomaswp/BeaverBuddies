# Multiplayer Maps

BeaverBuddies supports maps with multiple starting locations, allowing each player to begin with their own district. This page covers the multi-start system, map editor extensions, and how multiplayer metadata is stored in map files.

## Multiple Starting Locations

In standard Timberborn, each map has a single starting location where all initial buildings and beavers spawn. BeaverBuddies extends this so a map can have up to 4 starting locations, one per player.

When a multiplayer map loads:

1. Each starting location spawns its own set of starting buildings.
2. Each starting location spawns its own set of beavers.
3. The camera centers on the first player's location.
4. Players share the same game world but begin in separate areas.

If a map has more starting locations than the configured player count, only the first N locations are used (ordered by player index). If a map has only one starting location, the standard single-player behavior is used.

## MultiStart Directory

The `BeaverBuddies/MultiStart/` directory implements the core multi-start gameplay logic.

### StartingLocationPlayer

**File:** `BeaverBuddies/Editor/StartingLocationPlayer.cs`

`StartingLocationPlayer` is a component added to starting location entities. It tracks which player owns each location.

**Key fields and constants:**

| Field | Description |
|---|---|
| `PlayerIndex` | Integer 0-3 identifying the owning player |
| `MAX_PLAYERS` | Constant: `4` |
| `DEFAULT_MAX_STARTING_LOCS` | Constant: `2` |
| `PLAYER_COLORS` | Array of 4 `Color` values for visual differentiation |

**Player colors:**

| Index | Color Description |
|---|---|
| 0 | Warm rose (lightened `#770018`) |
| 1 | Teal-green (lightened `#00775f`) |
| 2 | Purple (lightened `#5f0077`) |
| 3 | Gold (`#f5bd02`) |

The component implements `IPersistentEntity`, saving and loading `PlayerIndex` via the `"StartingLocationPlayer"` component key. On `Start()`, it tints the starting location's renderer materials to the player's color.

A companion record, `StartingLocationPlayerSpec`, extends `ComponentSpec` to carry `PlayerIndex` through the template/blueprint system.

### StartBuildingsService

**File:** `BeaverBuddies/MultiStart/StartBuildingsService.cs`

`StartBuildingsService` is a `RegisteredSingleton` that tracks which buildings were spawned at starting locations.

```csharp
public class StartBuildingsService : RegisteredSingleton
{
    public List<Building> StartingBuildings { get; private set; } = new List<Building>();
    public bool IsMultiStart => StartingBuildings.Count > 1;

    public int MaxStartLocations()
    {
        GameModeSpec newGameMode = ...;
        if (newGameMode is MultiplayerNewGameModeSpec multiplayerNewGameMode)
        {
            return multiplayerNewGameMode.Players;
        }
        return StartingLocationPlayer.DEFAULT_MAX_STARTING_LOCS;
    }
}
```

- `RegisterStartingBuilding()` -- Called during initialization for each spawned starting building.
- `IsMultiStart` -- Returns true when more than one starting building was registered.
- `MaxStartLocations()` -- Reads the player count from the `MultiplayerNewGameModeSpec` if available, otherwise defaults to `DEFAULT_MAX_STARTING_LOCS` (2).

### MultiplayerNewGameModeSpec

**File:** `BeaverBuddies/MultiStart/MultiplayerNewGameMode.cs`

A record type extending Timberborn's `GameModeSpec` with a `Players` property:

```csharp
public record MultiplayerNewGameModeSpec : GameModeSpec
{
    public int Players { get; init; }

    public MultiplayerNewGameModeSpec(GameModeSpec mode, int players) : base(mode)
    {
        Players = players;
    }
}
```

This is created in the new game configuration UI when the player sets the number of starting locations.

### MultiStartPatches

**File:** `BeaverBuddies/MultiStart/MultiStartPatches.cs`

This file contains the Harmony patches that make multi-start work.

#### StartingBuildingInitializerInitializePatcher

Patches `StartingBuildingInitializer.Initialize` to handle multiple starting locations:

1. Retrieves all `StartingLocation` entities from the entity registry.
2. If only one exists, defers to default behavior.
3. Orders locations by `StartingLocationPlayer.PlayerIndex`.
4. For each location (up to `MaxStartLocations`):
   - Calls `_startingBuildingSpawner.Place()` with that location's placement.
   - Registers the building with `StartBuildingsService`.
5. Centers the camera on the first location.
6. Deletes all starting location markers.

#### GameInitializerSpawnBeaversPatcher

Patches `GameInitializer.SpawnBeavers` to spawn beavers at each starting building rather than just one:

```csharp
foreach (Building startingBuilding in startBuildingsService.StartingBuildings)
{
    Vector3? unblockedSingleAccess = startingBuilding
        .GetComponent<BuildingAccessible>().Accessible.UnblockedSingleAccess;
    if (unblockedSingleAccess.HasValue)
    {
        __instance._startingBeaverInitializer.Initialize(
            valueOrDefault, newGameMode.StartingAdults, ...);
    }
}
```

Each starting building gets its own set of adult and child beavers.

#### CustomNewGameModeControllerInitializePatcher

Patches the new game mode customization UI to add a **"Max Starting Locations"** integer field. This field:

- Appears above the "Starting Adults" field.
- Defaults to `DEFAULT_MAX_STARTING_LOCS` (2).
- Has a minimum of 1 and maximum of `MAX_PLAYERS` (4).
- Is styled with bold text.
- Is stored in the `MultiplayerNewGameModeSpec` when the game mode is created.

#### CustomNewGameModeControllerGetNewGameModePatcher

Patches `CustomNewGameModeController.GetGameMode` to wrap the result in a `MultiplayerNewGameModeSpec` with the selected player count.

#### NewGameModePanel Patches

Two patches (`NewGameModePanelSelectModeButtonPatcher` and `NewGameModePanelSelectFactionAndMapPatcher`) auto-open the customization panel when a multiplayer map is selected, so the player can configure the number of starting locations.

### MultiStartConfigurator

**File:** `BeaverBuddies/MultiStart/MultiStartConfigurator.cs`

Registers the multi-start services with the dependency injection container:

```csharp
public static void Configure(IContainerDefinition containerDefinition)
{
    containerDefinition.Bind<StartBuildingsService>().AsSingleton();
    containerDefinition.Bind<StartingLocationPlayer>().AsTransient();
    containerDefinition.MultiBind<TemplateModule>()
        .ToProvider<TemplateModuleProvider>().AsSingleton();
}
```

The `TemplateModuleProvider` adds `StartingLocationPlayer` as a decorator for `StartingLocationSpec`, so every starting location entity automatically gets the player assignment component.

## Map Editor Extensions

The `BeaverBuddies/Editor/` directory extends Timberborn's map editor to support creating multiplayer maps.

### EditorConfigurator

**File:** `BeaverBuddies/Editor/EditorConfigurator.cs`

Registered with the `[Context("MapEditor")]` attribute, this configurator binds:

- `StartingLocationNumberService` -- for managing player numbering.
- All `MultiStartConfigurator` services -- so starting locations work in the editor.

### StartingLocationNumberService

**File:** `BeaverBuddies/Editor/StartingLocationNumberService.cs`

Manages the ordering and numbering of starting locations in the editor:

- `ResetNumbering()` -- Re-indexes all `StartingLocationPlayer` components sequentially (0, 1, 2, ...) ordered by their current `PlayerIndex`.
- `GetMaxPlayers()` -- Returns the count of starting locations (minimum 1).

### Map Editor Buttons

`MapEditorButtonsGetElementsPatcher` (in `MapEditorPatches.cs`) replaces the default starting location button with a tool group containing up to `MAX_PLAYERS` (4) individually labeled starting locations:

- "Starting Location 1" through "Starting Location 4"
- Each carries a `StartingLocationPlayerSpec` with the appropriate `PlayerIndex`.
- The blueprints are cloned from the original starting location blueprint with modified specs.

### Starting Location Deletion

`StartingLocationServiceDeleteOtherStartingLocationsPatcher` modifies the default behavior that deletes all but one starting location. In multiplayer maps, it only deletes other starting locations with the **same player index**, allowing multiple player-specific locations to coexist.

### Duplication Prevention

`DuplicationValidatorCanDuplicateObjectPatcher` prevents duplicating starting location entities with the duplication tool, since they carry player-specific data.

### MultiplayerMapMetadata

**File:** `BeaverBuddies/Editor/MultiplayerMapMetadata.cs`

Extends Timberborn's `MapMetadata` with a `MaxPlayers` property:

```csharp
public class MultiplayerMapMetadata : MapMetadata
{
    public int MaxPlayers { get; }

    public MultiplayerMapMetadata(MapMetadata metadata, int maxPlayers)
        : base(metadata.Width, metadata.Height, ...) { ... }
}
```

### MultiplayerMapMetadataService

Also in `MultiplayerMapMetadata.cs`, this `RegisteredSingleton` reads multiplayer metadata from map files:

```csharp
public MultiplayerMapMetadata TryGetMultiplayerMapMetadata(MapFileReference reference)
{
    return _mapDeserializer.ReadFromMapFile(reference, _mapMetadataSerializer)
        as MultiplayerMapMetadata;
}
```

Returns `null` for non-multiplayer maps.

### Serialization Patches

Two patches in `MapEditorPatches.cs` handle saving and loading the `MaxPlayers` field:

- `MapMetadataSerializerGetMapMetadataSerializedObjectPatcher` -- Adds `"MaxPlayers"` to the serialized object when saving a `MultiplayerMapMetadata`.
- `MapMetadataSerializerDeserializePatcher` -- Reads `"MaxPlayers"` during deserialization and wraps the result in a `MultiplayerMapMetadata`.
- `MapMetadataSaveEntryWriterCreateMapMetadataPatcher` -- On save, calls `ResetNumbering()` and wraps the metadata with the current max players count.

### Thumbnail Rendering

`StartingLocationThumbnailRenderingListenerPreThumnailRenderingPatcher` prevents starting locations from being hidden during thumbnail capture, so multiplayer map thumbnails show where each player starts.
