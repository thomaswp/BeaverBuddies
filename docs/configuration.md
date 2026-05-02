# Configuration

This page covers BeaverBuddies settings, build configurations, environment setup, localization, and assembly publicizing.

## Settings Class

**File:** `BeaverBuddies/Settings.cs`

`Settings` extends `ModSettingsOwner` (from the ModSettings mod) and exposes all configurable options through the Timberborn mod settings UI.

### Connection Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `ClientConnectionAddress` | `string` | `"127.0.0.1"` | Last-used server address for joining games |
| `DefaultPort` | `int` | `25565` | TCP port for direct connections |

### Steam Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `EnableSteamConnection` | `bool` | `true` | Whether to enable Steam P2P connectivity |
| `FriendsCanJoinSteamGame` | `bool` | `true` | Whether Steam friends can join without an invite |

### Developer Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `AlwaysTrace` | `bool` | `false` | Enable detailed desync tracing (see [Determinism](determinism.md)) |
| `SilenceLogging` | `bool` | `false` | Suppress verbose log output |

### User Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| `ShowFirstTimerMessage` | `bool` | `true` | Show welcome dialog on first load |
| `ReportingConsent` | `bool` | `false` | Allow automated desync report submission |

### Static Accessors

For convenience, `Settings` provides static properties that can be accessed without a reference to the singleton instance:

```csharp
public static bool Debug => TemporarilyDebug || (instance?.AlwaysTrace.Value ?? false);
public static bool VerboseLogging => !(instance?.SilenceLogging.Value == true);
public static int Port => instance?.DefaultPort.Value ?? 25565;
public static bool EnableSteam => instance?.EnableSteamConnection.Value ?? true;
public static bool LobbyJoinable => instance?.FriendsCanJoinSteamGame.Value ?? true;
```

### TemporarilyDebug

`Settings.TemporarilyDebug` is a non-persisted static flag. When set to `true`, it enables `Settings.Debug` for the current session without modifying the saved `AlwaysTrace` setting. This is useful for one-time debugging -- see [Testing and Debugging](testing.md).

## ConfigIOService (Legacy Migration)

**File:** `BeaverBuddies/ConfigIOService.cs`

`ConfigIOService` is an `IPostLoadableSingleton` that handles migration from the legacy `ReplayConfig.json` file to the modern ModSettings system.

On `PostLoad()`, it checks for a `ReplayConfig.json` file in the mod directory. If found:

1. Reads the JSON into a `ReplayConfig` object.
2. Transfers each field to the corresponding `ModSetting`:
   - `ClientConnectionAddress` -> `Settings.ClientConnectionAddress`
   - `Port` -> `Settings.DefaultPort`
   - `Verbose` -> `Settings.SilenceLogging` (inverted)
   - `FirstTimer` -> `Settings.ShowFirstTimerMessage`
   - `ReportingConsent` -> `Settings.ReportingConsent`
   - `AlwaysDebug` -> `Settings.AlwaysTrace`
3. Deletes the old config file.

This ensures a smooth upgrade path for existing users.

## Build Configurations

**File:** `BeaverBuddies/BeaverBuddies.csproj`

The project defines four build configurations:

| Configuration | Description |
|---|---|
| `Debug` | Standard debug build without Steam features |
| `Release` | Optimized build without Steam features |
| `Debug Steam` | Debug build with Steam integration |
| `Release Steam` | Release build with Steam integration |

The Steam configurations define the `IS_STEAM` preprocessor symbol:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug Steam' OR '$(Configuration)' == 'Release Steam'">
  <DefineConstants>$(DefineConstants);IS_STEAM</DefineConstants>
</PropertyGroup>
```

Code that depends on Steam is wrapped in `#if IS_STEAM` / `#endif` blocks. This includes:

- `SteamOverlayConnectionService` (lobby management and join handling)
- `SteamOverlayInputBlockerPatch` (overlay input blocking)
- Steam listener creation in `ServerEventIO`

The non-Steam builds compile stub implementations (e.g., empty `UpdateSingleton()`) to satisfy interface requirements.

## env.props Reference

The build system uses an `env.props` MSBuild file to locate system-specific paths. If `env.props` does not exist, the build automatically copies from the appropriate template (`env.props.windows-template` or `env.props.unix-template`).

### Required Properties

| Property | Description |
|---|---|
| `TimberbornDataPath` | Path to Timberborn's data directory (containing `Managed/`) |
| `BepInExPath` | Path to BepInEx installation (containing `core/`) |
| `ModSettingsPath` | Path to the ModSettings mod's `Scripts/` directory |
| `DocumentsPath` | User documents directory (where `Timberborn/Mods/` lives) |

All paths must end with a trailing slash.

### Windows Example (env.props.windows-template)

```xml
<TimberbornDataPath>C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\</TimberbornDataPath>
<BepInExPath>C:\Dev\BepInEx\</BepInExPath>
<ModSettingsPath>C:\Program Files (x86)\Steam\steamapps\workshop\content\1062090\3283831040\version-0.7\Scripts\</ModSettingsPath>
<DocumentsPath>$(registry:HKEY_CURRENT_USER\...\User Shell Folders@Personal)\</DocumentsPath>
```

Note: The Windows template reads `DocumentsPath` from the registry to handle non-standard document folder locations.

### macOS Example (env.props.unix-template)

```xml
<TimberbornDataPath>$(HOME)/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/</TimberbornDataPath>
<BepInExPath>$(HOME)/dev/BepInEx/BepInEx/</BepInExPath>
<ModSettingsPath>$(HOME)/Documents/Timberborn/Mods/modsettings-ey0f/version-1.0/Scripts/</ModSettingsPath>
<DocumentsPath>$(HOME)/Documents/</DocumentsPath>
```

### Build Validation

The `CheckEnv` target runs before each build and verifies all four directories exist:

```xml
<Error Text="TimberbornDataPath property directory not found" Condition="!Exists('$(TimberbornDataPath)')" />
<Error Text="BepInExPath property directory not found" Condition="!Exists('$(BepInExPath)')" />
<Error Text="ModSettingsPath property directory not found" Condition="!Exists('$(ModSettingsPath)')" />
<Error Text="DocumentsPath property directory not found" Condition="!Exists('$(DocumentsPath)')" />
```

### Output Deployment

The `PostBuild` target copies the compiled mod to `$(DocumentsPath)Timberborn\Mods\BeaverBuddies\`, making it immediately available in Timberborn.

## Assembly Publicizing

BeaverBuddies needs access to many private and internal members of Timberborn's assemblies. Rather than using reflection everywhere, the project uses `BepInEx.AssemblyPublicizer.MSBuild`:

```xml
<PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3">
  <PrivateAssets>all</PrivateAssets>
  <PublicizeCompilerGenerated>false</PublicizeCompilerGenerated>
</PackageReference>
```

Assembly references that need publicizing are marked with `Publicize="true"`:

```xml
<Reference Include="$(TimberbornManagedPath)UnityEngine.*.dll" Publicize="true" Private="false" />
<Reference Include="$(TimberbornManagedPath)Timberborn.*.dll" Publicize="true" Private="false" />
```

This makes all private fields, methods, and properties accessible at compile time. Code that relies on this access is annotated with `[ManualMethodOverwrite]` to flag methods that duplicate Timberborn internals and may need updating when the game updates.

## Localization

### Localization Files

BeaverBuddies ships localization CSV files in the `BeaverBuddies/Localizations/` directory. Currently supported languages:

| File | Language |
|---|---|
| `enUS_BeaverBuddie.csv` | English (US) |
| `deDE_BeaverBuddie.csv` | German |
| `esES_BeaverBuddie.csv` | Spanish |
| `frFR_BeaverBuddie.csv` | French |
| `itIT_BeaverBuddie.csv` | Italian |
| `jaJP_BeaverBuddie.csv` | Japanese |
| `koKR_BeaverBuddie.csv` | Korean |
| `plPL_BeaverBuddie.csv` | Polish |
| `ptBR_BeaverBuddie.csv` | Portuguese (Brazil) |
| `ruRU_BeaverBuddie.csv` | Russian |
| `thTH_BeaverBuddie.csv` | Thai |
| `trTR_BeaverBuddie.csv` | Turkish |
| `ukUA_BeaverBuddie.csv` | Ukrainian |
| `zhCN_BeaverBuddie.csv` | Chinese (Simplified) |
| `zhTW_BeaverBuddie.csv` | Chinese (Traditional) |

These files are copied to the output directory during build and deployed with the mod.

### RegisteredLocalizationService

**File:** `BeaverBuddies/Util/RegisteredLocalizationService.cs`

A thin wrapper around Timberborn's `ILoc` interface, registered as a singleton:

```csharp
public class RegisteredLocalizationService : RegisteredSingleton
{
    public ILoc ILoc { get; private set; }

    public static string T(string key)
    {
        return SingletonManager.GetSingleton<RegisteredLocalizationService>().ILoc.T(key);
    }
}
```

The static `T()` method provides convenient access to localized strings from anywhere in the codebase, including Harmony patches and static contexts where dependency injection is not available. Localization keys follow the pattern `BeaverBuddies.<Category>.<Key>`.

### Updating Localizations

The `Inspector` project includes an `UpdateLocalizations` utility (see [Testing and Debugging](testing.md)) that parses structured translation input and merges new entries into the CSV files.
