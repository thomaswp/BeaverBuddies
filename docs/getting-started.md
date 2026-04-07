# Getting Started

This guide walks you through setting up a development environment for BeaverBuddies.

## Prerequisites

- **Timberborn** - The base game (Steam or other distribution)
- **Visual Studio 2022** - [Community Edition](https://visualstudio.microsoft.com/downloads/) (free)
- **BepInEx v5.4.22** - [Download here](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.22)
- **ModSettings mod** - Required dependency ([Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3283831040))

## Step 1: Install BepInEx

1. Download the correct BepInEx zip for your platform from the link above
2. Extract the contents into your Timberborn game folder:
   - **Windows:** `C:\Program Files (x86)\Steam\steamapps\common\Timberborn\`
   - **macOS:** `~/Library/Application Support/Steam/steamapps/common/Timberborn/`
3. Run Timberborn once to finish BepInEx installation. If successful, you'll see a `config` folder inside `BepInEx/`

**Recommended:** Enable console logging for debugging by editing `BepInEx/config/BepInEx.cfg`:

```ini
[Logging.Console]

## Enables showing a console for log output.
# Setting type: Boolean
# Default value: false
Enabled = true
```

## Step 2: Clone and Configure

1. Clone the repository:

```bash
git clone https://github.com/matthewwachter/BeaverBuddies.git
cd BeaverBuddies
```

2. Open `BeaverBuddies.sln` in Visual Studio 2022

3. On first build, `env.props` is automatically created from the platform template. If needed, copy it manually:
   - **Windows:** Copy `env.props.windows-template` to `env.props`
   - **macOS/Linux:** Copy `env.props.unix-template` to `env.props`

4. Edit `BeaverBuddies/env.props` to match your system paths:

**Windows:**

```xml
<Project>
  <PropertyGroup>
    <!-- All paths must end with a slash -->
    <TimberbornDataPath>C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\</TimberbornDataPath>
    <BepInExPath>C:\Dev\BepInEx\</BepInExPath>
    <ModSettingsPath>C:\Program Files (x86)\Steam\steamapps\workshop\content\1062090\3283831040\version-0.7\Scripts\</ModSettingsPath>
    <DocumentsPath>C:\Users\YourName\Documents\</DocumentsPath>
  </PropertyGroup>
</Project>
```

**macOS:**

```xml
<Project>
  <PropertyGroup>
    <TimberbornDataPath>$(HOME)/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/</TimberbornDataPath>
    <BepInExPath>$(HOME)/dev/BepInEx/BepInEx/</BepInExPath>
    <ModSettingsPath>$(HOME)/Documents/Timberborn/Mods/modsettings-ey0f/version-1.0/Scripts/</ModSettingsPath>
    <DocumentsPath>$(HOME)/Documents/</DocumentsPath>
  </PropertyGroup>
</Project>
```

### Path Reference

| Property | Description |
|----------|-------------|
| `TimberbornDataPath` | Points to the game's `Timberborn_Data/` (or equivalent on macOS) directory |
| `BepInExPath` | Root BepInEx directory containing `core/` with Harmony DLLs |
| `ModSettingsPath` | ModSettings mod `Scripts/` directory containing its DLLs |
| `DocumentsPath` | User documents folder where `Timberborn/Mods/` lives |

## Step 3: Build

Build the project with `Ctrl+Shift+B` (or `dotnet build` from the command line).

On successful build, the post-build step automatically copies the mod files to:

```
Documents/Timberborn/Mods/BeaverBuddies/
```

This includes the compiled DLL, manifest, localizations, and thumbnail.

## Step 4: Run

1. Launch Timberborn
2. On the mod selection screen, enable the **BeaverBuddies** mod
3. Start or load a game

## Build Configurations

| Configuration | Description |
|---------------|-------------|
| **Debug** | Standard debug build with symbols |
| **Release** | Optimized release build |
| **Debug Steam** | Debug build with `IS_STEAM` defined - includes Steam P2P networking |
| **Release Steam** | Release build with `IS_STEAM` defined |

The `IS_STEAM` conditional compilation symbol enables Steam-specific code paths (P2P networking, lobby system, overlay integration). Non-Steam builds only support direct TCP connections.

## Solution Structure

The solution contains four projects:

| Project | Type | Description |
|---------|------|-------------|
| **BeaverBuddies** | .NET Standard 2.1 Library | Main mod - BepInEx plugin with all game patches and multiplayer logic |
| **TimberNet** | .NET Standard 2.1 Library | Standalone networking library (TCP/Steam transport, message framing) |
| **ClientServerSimulator** | Windows Forms App | Testing tool for simulating client/server communication |
| **Inspector** | Console App | Utility tools (localization updates, etc.) |

## Key Dependencies

- **BepInEx.AssemblyPublicizer.MSBuild** - Makes private Timberborn fields/methods accessible at compile time
- **Timberborn DLLs** - Referenced from game installation (publicized for access to internals)
- **Bindito** - Timberborn's dependency injection framework
- **HarmonyX** - Runtime method patching for intercepting game logic
- **Steamworks.NET** - Steam API wrapper (Steam builds only)
- **Newtonsoft.Json** - JSON serialization (included via TimberNet)
- **System.Collections.Immutable** - Immutable collections (NuGet)
