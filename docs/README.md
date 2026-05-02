# BeaverBuddies

**Multiplayer Co-Op Mod for Timberborn**

BeaverBuddies transforms Timberborn from a single-player city builder into a cooperative multiplayer experience. Multiple players can build and manage a beaver settlement together in real-time.

[![BeaverBuddies Demo](https://github.com/thomaswp/BeaverBuddies/assets/1750176/41bb5d71-ff64-493c-a362-3c2483910b7d)](https://www.youtube.com/watch?v=m56GVA7XbI8)

> **Players**: Check the [User Guide](user-guide) for installation and setup instructions.
>
> **Developers**: This documentation covers the full codebase architecture and internals.

## Quick Links

- [Getting Started](getting-started) - Set up your development environment
- [Architecture Overview](architecture) - Understand how the mod is structured
- [Contributing](contributing) - Learn how to contribute to the project
- [Event System](event-system) - How player actions are synchronized
- [Networking & IO](networking) - The network protocol and transport layer

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Language | C# (.NET Standard 2.1) |
| Game Modding | BepInEx 5.4.22 |
| Runtime Patching | HarmonyX |
| Dependency Injection | Bindito |
| Networking | Custom TCP + Steam P2P (Steamworks.NET) |
| Serialization | Newtonsoft.Json |
| Settings | ModSettings framework |
| Build System | MSBuild / Visual Studio 2022 |

## How It Works

BeaverBuddies uses an **event sourcing** architecture to synchronize game state across players:

1. All player actions (building, demolition, speed changes, etc.) are captured as **events** via Harmony patches
2. Events are serialized to JSON and transmitted over the network
3. All clients **replay** events deterministically so their game states stay in sync
4. A **tick synchronization** system ensures all clients advance the simulation in lockstep
5. **Desync detection** catches divergences and can auto-report them for debugging

## Project Structure

```
BeaverBuddies/              # Main mod (BepInEx plugin)
  Connect/                   # Connection management & UI
  DesyncDetecter/            # Desync detection & tracing
  Editor/                    # Map editor extensions
  Events/                    # Event definitions & Harmony patches
  Fixes/                     # Determinism fixes for game systems
  IO/                        # Event IO abstraction (network & file)
  MultiStart/                # Multiple player starting locations
  Reporting/                 # Error reporting
  Steam/                     # Steam P2P networking & overlay
  Util/                      # Utilities & logging
TimberNet/                   # Standalone networking library
ClientServerSimulator/       # Testing tool (Windows Forms)
Inspector/                   # Utility tools
```

## Current Version

**v1.6.6** - Minimum Timberborn version: **0.7.2.0**
