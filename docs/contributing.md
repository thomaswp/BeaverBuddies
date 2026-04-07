# Contributing

This guide covers how to contribute to BeaverBuddies, including code conventions, common workflows, and how to add new synchronized actions.

## Development Workflow

1. Fork the repository and clone your fork
2. Follow the [Getting Started](getting-started) guide to set up your environment
3. Create a feature branch from `dev`
4. Implement your changes
5. Test with two game instances (host + client) or the ClientServerSimulator
6. Submit a pull request to `dev`

## Code Conventions

### Naming

- **PascalCase** for public members, types, and methods
- **camelCase** for private fields and local variables
- **_underscore prefix** for injected dependencies (e.g., `_replayService`)
- Harmony patch classes: `{TargetClass}{Method}Patcher` (e.g., `SpeedManagerChangeSpeedPatcher`)

### Harmony Patches

BeaverBuddies uses HarmonyX to intercept Timberborn game methods at runtime. Key conventions:

- Prefix patches are the primary mechanism for capturing player actions
- Use `[HarmonyPatch]` attributes on patch classes
- Patch methods are typically `static bool Prefix(...)` returning whether the original method should execute

### ManualMethodOverwrite

When a Harmony patch must copy and modify Timberborn source code (rather than just wrapping it), mark it with the `[ManualMethodOverwrite]` attribute. This signals that the patch contains game code that may break when Timberborn updates.

When updating for a new Timberborn version, search for all `[ManualMethodOverwrite]` usages and verify the copied code still matches the game's current implementation.

## Adding a New Synced Action

The most common contribution is adding synchronization for a game action that isn't yet replicated in multiplayer. Here's the pattern:

### 1. Create a ReplayEvent Subclass

```csharp
public class MyNewEvent : ReplayEvent
{
    public string someData;

    public override void Replay(IReplayContext context)
    {
        // Execute the action during replay
        var service = context.GetSingleton<SomeService>();
        service.DoSomething(someData);
    }
}
```

### 2. Write a Harmony Prefix Patch

```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.TargetMethod))]
public static class TargetClassTargetMethodPatcher
{
    public static bool Prefix(TargetClass __instance, string parameter)
    {
        return ReplayEvent.DoPrefix(
            () => new MyNewEvent
            {
                someData = parameter
            }
        );
    }
}
```

The `DoPrefix()` helper handles the full lifecycle:
- If this is normal gameplay: creates the event, records it, and returns `true` (letting the original method run)
- If this is during replay: returns `false` (the event's `Replay()` method handles execution instead)
- If replay service isn't ready: returns `true` (lets the game run normally in single-player)

### 3. For Entity-Based Actions

If the action targets a specific game entity (building, beaver, etc.), use `DoEntityPrefix`:

```csharp
public static bool Prefix(BuildingComponent __instance, int value)
{
    return ReplayEvent.DoEntityPrefix(
        __instance,
        () => new MyEntityEvent
        {
            entityID = __instance.GetComponentFast<EntityComponent>().EntityId,
            newValue = value
        }
    );
}
```

### 4. Test Your Changes

1. **Two-instance test:** Host a game on one Timberborn instance, join from another. Perform the action on both sides and verify it syncs correctly.
2. **ClientServerSimulator:** For automated testing, create JSON event scripts and use the simulator (see [Testing & Debugging](testing)).
3. **Desync check:** Play for several minutes after your action to verify no desyncs occur. Enable `AlwaysTrace` in settings for detailed tracing.

## Diagnosing Desyncs

When a desync occurs, it means the game state has diverged between server and client. Common causes:

1. **Missing event capture** - A game action modifies state but isn't intercepted by a Harmony patch
2. **Non-deterministic code** - Code that uses `Time.time`, `Random` outside the seeded RNG, or other frame-dependent values
3. **Parallel tick ordering** - Systems that tick in parallel may execute in different orders
4. **Floating point divergence** - Accumulated rounding differences between machines

### Debugging Workflow

1. Enable `AlwaysTrace` in mod settings
2. Reproduce the desync
3. Check the BepInEx console/log for trace output
4. Compare server and client traces to find where they diverge
5. The divergence point indicates which system or event caused the desync

See [Determinism & Desync Detection](determinism) for detailed information on the detection system.

## Updating for New Timberborn Versions

When Timberborn releases an update:

1. Update game DLL references in your `env.props` if paths changed
2. Build and check for compile errors (API changes)
3. Search for `[ManualMethodOverwrite]` - each marks a patch that copies Timberborn code. Verify the copied code still matches the game's current version
4. Test all major features: hosting, joining, building, demolition, speed control, etc.
5. Run a full multiplayer session to check for new desyncs
