using HarmonyLib;
using System;
using Timberborn.BehaviorSystem;
using Timberborn.BlockSystem;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.NaturalResources;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.NaturalResourcesReproduction;
using Timberborn.ReservableSystem;
using Timberborn.SlotSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WalkingSystem;
using Timberborn.WaterSystem;
using UnityEngine;
using static Timberborn.NaturalResourcesReproduction.NaturalResourceReproducer;

namespace BeaverBuddies.DesyncDetecter
{

    [HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.MarkSpots))]
    class NRPMarkSpotsPatcher
    {
        private static int lastCount;
        public static void Prefix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!Settings.Debug) return;
            if (!ReplayService.IsLoaded) return;
            var key = ReproducibleKey.Create(reproducible);
            lastCount = __instance._potentialSpots.ContainsKey(key) ? __instance._potentialSpots[key].Count : 0;
            DesyncDetecterService.Trace($"Marking spots for   {reproducible.Id} at {reproducible.GetComponent<BlockObject>().Coordinates} ({reproducible.GetComponent<EntityComponent>().EntityId})");
        }

        public static void Postfix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!Settings.Debug) return;
            if (!ReplayService.IsLoaded) return;
            var key = ReproducibleKey.Create(reproducible);
            int count = __instance._potentialSpots.ContainsKey(key) ? __instance._potentialSpots[key].Count : 0;
            DesyncDetecterService.Trace($"Spots updated: {lastCount} --> {count}");
        }
    }

    [HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.UnmarkSpots))]
    class NRPUnmarkSpotsPatcher
    {
        private static int lastCount;
        static void Prefix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!Settings.Debug) return;
            if (!ReplayService.IsLoaded) return;
            var key = ReproducibleKey.Create(reproducible);
            lastCount = __instance._potentialSpots.ContainsKey(key) ? __instance._potentialSpots[key].Count : 0; if (!ReplayService.IsLoaded) return;
            DesyncDetecterService.Trace($"Unmarking spots for   {reproducible.Id} at {reproducible.GetComponent<BlockObject>().Coordinates} ({reproducible.GetComponent<EntityComponent>().EntityId})");
        }

        static void Postfix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!Settings.Debug) return;
            if (!ReplayService.IsLoaded) return;
            var key = ReproducibleKey.Create(reproducible);
            int count = __instance._potentialSpots.ContainsKey(key) ? __instance._potentialSpots[key].Count : 0;
            DesyncDetecterService.Trace($"Spots updated: {lastCount} --> {count}");
        }
    }

    // Non-game things create TimeTriggers, even though they happen during the tick
    // logic, so probably best to exclude.
    //[HarmonyPatch(typeof(TimeTriggerService), nameof(TimeTriggerService.Trigger), typeof(TimeTrigger))]
    //class TimeTriggerServiceTriggerPatcher
    //{
    //    static void Prefix(TimeTriggerService __instance, TimeTrigger timeTrigger)
    //    {
    //        float triggerTime = 0;
    //        long id = 0;
    //        if (__instance._timeTriggerKeys.TryGetValue(timeTrigger, out var key))
    //        {
    //            triggerTime = key.Timestamp;
    //            id = key._id;
    //        }
    //        DesyncDetecterService.Trace($"Triggering time trigger at {__instance._dayNightCycle.PartialDayNumber}: {id}-{triggerTime}");
    //    }
    //}

    //[HarmonyPatch(typeof(TimeTriggerService), nameof(TimeTriggerService.Add))]
    //class TimeTriggerServiceAddPatcher
    //{
    //    static void Prefix(TimeTriggerService __instance, TimeTrigger timeTrigger, float triggerTimestamp)
    //    {
    //        // Remove this to see loading timers; should be deterministic now but could test
    //        // in the future if something's not working. For now this removes triggers that
    //        // aren't a part of tick logic and *shouldn't* affect gameplay.
    //        if (!DeterminismService.IsTicking) return;
    //        DesyncDetecterService.Trace($"Adding time trigger at {__instance._nextId}-{triggerTimestamp}; ticking: {DeterminismService.IsTicking}");
    //    }
    //}

    [HarmonyPatch(typeof(SpawnValidationService), nameof(SpawnValidationService.CanSpawn))]
    class SpawnValidationServiceCanSpawnPatcher
    {
        public static void Postfix(SpawnValidationService __instance, bool __result, Vector3Int coordinates, BlockObjectSpec blockObjectSpec, string resourceId)
        {
            if (!Settings.Debug) return;
            DesyncDetecterService.Trace($"Trying to spawn {resourceId} at {coordinates}: {__result}\n" +
                $"IsSuitableTerrain: {__instance.IsSuitableTerrain(coordinates)}\n" +
                $"SpotIsValid: {__instance.SpotIsValid(coordinates, resourceId)}\n" +
                $"IsUnobstructed: {__instance.IsUnobstructed(coordinates, resourceId)}");
        }
    }

    [HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.SpawnNewResources))]
    public class NRRPatcher
    {
        public static void Prefix(NaturalResourceReproducer __instance)
        {
            if (!Settings.Debug) return;
            foreach (var (reproducibleKey, coordinates) in __instance._newResources)
            {
                DesyncDetecterService.Trace($"Spawning: {reproducibleKey.Id}, {coordinates}");
            }
        }
    }


    [HarmonyPatch(typeof(Walker), nameof(Walker.FindPath))]
    public class WalkerFindPathPatcher
    {

        public static void Prefix(Walker __instance, IDestination destination)
        {
            if (!Settings.Debug) return;
            string entityID = __instance.GetComponent<EntityComponent>().EntityId.ToString();
            string destinationString = GetDestinationString(destination);
            DesyncDetecterService.Trace($"{entityID} going to: {destinationString}");
        }

        public static string GetDestinationString(IDestination destination)
        {
            if (destination == null) return "null";
            string destinationString = null;
            if (destination is PositionDestination)
            {
                destinationString = ((PositionDestination)destination)?.Destination.ToString();
            }
            else if (destination is AccessibleDestination)
            {
                var accessible = ((AccessibleDestination)destination).Accessible;
                // Manually check since MonoBehavior doesn't support null conditional operator
                if (accessible != null) destinationString = accessible?.GameObject?.name;
            }
            if (destinationString == null) destinationString = destination?.GetType().Name;
            return destinationString;
        }

        public static void Postfix(Walker __instance, IDestination destination, ExecutorStatus __result)
        {
            if (!Settings.Debug) return;
            string entityID = __instance.GetComponent<EntityComponent>().EntityId.ToString();
            bool arrived = false;
            if (__instance._currentDestination != null)
            {
                arrived = __instance.IsOutsideAndReachedDestination();
            }
            DesyncDetecterService.Trace($"{entityID} finished pathfinding; " +
                $"reachable = { __instance.CurrentDestinationReachable }; result: {__result}");
        }
    }

    // In theory this is deterministic based on the model's GUID, so it shouldn't diverge
    [HarmonyPatch(typeof(NaturalResourceModelRandomizer), nameof(NaturalResourceModelRandomizer.RandomizeDiameterScale))]
    public class NaturalResourceModelRandomizerPatcher
    {
        public static void Postfix(NaturalResourceModelRandomizer __instance)
        {
            if (!Settings.Debug) return;
            var id = __instance.GetComponent<EntityComponent>().EntityId;
            DesyncDetecterService.Trace($"NaturalResourceModelRandomizer {id} randomizing diameter scale to {__instance.DiameterScale}");
        }
    }

    [HarmonyPatch(typeof(WalkToReservableExecutor), nameof(WalkToReservableExecutor.Launch))]
    public class WalkToReservableExecutorPatcher
    {
        public static void Prefix(WalkToReservableExecutor __instance, ReservableReacher reservableReacher)
        {
            if (!Settings.Debug) return;
            var id = __instance.GetComponent<EntityComponent>().EntityId;
            DesyncDetecterService.Trace(
                $"WalkToReservableExecutor for {id} launching to " +
                $"{reservableReacher.GetType().Name} -> " +
                $"{WalkerFindPathPatcher.GetDestinationString(reservableReacher.Destination)}");
        }
    }


    [HarmonyPatch(typeof(WateredNaturalResource), nameof(WateredNaturalResource.StartDryingOut))]
    public class WateredNaturalResourceStartDryingOutPatcher
    {
        public static void Prefix(WateredNaturalResource __instance)
        {
            if (!Settings.Debug) return;
            var id = __instance.GetComponent<EntityComponent>().EntityId;
            var isDead = __instance._livingNaturalResource.IsDead;
            var time = ((TimeTrigger)__instance._timeTrigger)._delayLeftInDays;
            DesyncDetecterService.Trace(
                $"WateredNaturalResource {id} [dead={isDead}] starting to dry out; " +
                $"trigger delay = {time}");
        }
    }

    // TODO: This is too laggy, so it causes a desync from lag. Need to fix that to
    // see if I still get desyncs from not-lag.
    //[HarmonyPatch(typeof(SoilMoistureMap), nameof(SoilMoistureMap.SetMoistureLevel))]
    //public class SoilMoistureMapSetMoistureLevelPatcher
    //{
    //    public static void Prefix(Vector2Int coordinates, int index, float newLevel)
    //    {
    //        if (!Settings.Debug) return;
    //        DesyncDetecterService.Trace($"Setting moisture level for {coordinates} to {newLevel}");
    //    }
    //}

    [HarmonyPatch(typeof(SoilMoistureService), nameof(SoilMoistureService.UpdateMoistureLevels))]
    public class SoilMoistureMapSetMoistureLevelPatcher
    {
        public static void Postfix(SoilMoistureService __instance)
        {
            if (!Settings.Debug) return;
            // During saves, this gets called early, but it just syncs the
            // saveable Map with the already computed values, so when it's called
            // again during the next Tick's Singleton update (at the start of the frame)
            // it should *generally* not cause issues. In theory it could if a ReplayEvent
            // was replayed before the Singletons were ticked, or if another Singleton used
            // the values, but I'm guessing that doesn't happen... One solution if it does:
            // Just always update the moisture levels at the end of the tick.
            if (GameSaverSavePatcher.IsSaving) return;

            var levels = __instance._soilMoistureSimulator.MoistureLevels;
            int hash = 13;
            foreach (var level in levels)
            {
                hash = (hash * 7) + BitConverter.SingleToInt32Bits(level);
            }
            DesyncDetecterService.Trace($"Updating moisture levels with hash {hash:X8}");
        }
    }

    [HarmonyPatch(typeof(ThreadSafeWaterMap), nameof(ThreadSafeWaterMap.Update))]
    public class ThreadSafeWaterMapUpdateDataPatcher
    {
        public static void Postfix(ThreadSafeWaterMap __instance)
        {
            if (!Settings.Debug) return;

            var columns = __instance._threadSafeWaterColumns;
            int hash = 13;
            foreach (var level in columns)
            {
                hash = (hash * 7) + GetHashCode(level);
            }
            DesyncDetecterService.Trace($"Updating water map columns with hash {hash:X8}");
            
            hash = 13;
            var counts = __instance._threadSafeColumnCounts;
            foreach (byte count in counts)
            {
                hash = (hash * 7) + count;
            }
            DesyncDetecterService.Trace($"Updating water map column counts with hash {hash:X8}");
        }

        private static int GetHashCode(ReadOnlyWaterColumn waterColumn)
        {
            int hash = 13;
            hash = (hash * 7) + BitConverter.SingleToInt32Bits(waterColumn.Ceiling);
            hash = (hash * 7) + BitConverter.SingleToInt32Bits(waterColumn.Contamination);
            hash = (hash * 7) + BitConverter.SingleToInt32Bits(waterColumn.Floor);
            hash = (hash * 7) + BitConverter.SingleToInt32Bits(waterColumn.Overflow);
            hash = (hash * 7) + BitConverter.SingleToInt32Bits(waterColumn.WaterDepth);
            return hash;
        }
    }

    [HarmonyPatch(typeof(TickableEntityBucket), nameof(TickableEntityBucket.Add))]
    public class TEBAddPatcher
    {

        static void Postfix(TickableEntityBucket __instance, TickableEntity tickableEntity)
        {
            if (!ReplayService.IsLoaded) return;
            if (Settings.Debug)
            {
                int index = __instance._tickableEntities.Values.IndexOf(tickableEntity);
                DesyncDetecterService.Trace($"Adding: {tickableEntity.EntityId} at index {index}");
            }
            //Plugin.LogStackTrace();
        }
    }

    // This adds a prefix patch the Enterer.Enter method.
    [HarmonyPatch(typeof(Enterer), nameof(Enterer.Enter))]
    public class EntererEnterPatcher
    {
        // If we set the type to void, it won't interfere with the original method.
        // Note that the parameters have to match the original method's parameters exactly,
        // and we can also add a __instance parameter to get the instance of the class,
        // since this is a static method.
        static void Prefix(Enterer __instance, Enterable enterable)
        {
            // We always have to add this statement to ensure these don't happen unless detailed
            // logging is turned on, since they do add a performance cost.
            if (!Settings.Debug) return;

            // When possible, we want to parameterize the trace message with any details that might
            // diverge between the two games.
            // In general, try to write these in as null-safe a way as possible; we wouldn't
            // want a trace call to crash the game!
            var entererEntityId = __instance.GetComponent<EntityComponent>()?.EntityId;
            var enterableEntityId = enterable?.GetComponent<EntityComponent>()?.EntityId;
            var enterableName = enterable?.GameObject?.name;
            DesyncDetecterService.Trace($"Entity {entererEntityId} entering {enterableName} ({enterableEntityId})");
        }
    }

    [HarmonyPatch(typeof(SlotManager), nameof(SlotManager.AddEnterer))]
    public class SlotManagerAddEntererPatcher
    {
        static void Prefix(SlotManager __instance, Enterer enterer)
        {
            if (!Settings.Debug) return;
            var entererEntityId = enterer.GetComponent<EntityComponent>()?.EntityId;
            DesyncDetecterService.Trace($"SlotManager adding enterer {entererEntityId}");
        }
    }

    [HarmonyPatch(typeof(BehaviorManager), nameof(BehaviorManager.TickRunningExecutor))]
    public class BehaviorManagerTickRunningExecutorPatcher
    {
        static void Prefix(BehaviorManager __instance)
        {
            if (!Settings.Debug) return;
            var runningExecutorType = __instance._runningExecutor?.GetType().Name;
            var elapsedTime = __instance._runningExecutorElapsedTime;
            DesyncDetecterService.Trace($"BehaviorManager ticking executor {runningExecutorType} with last elapsed time {elapsedTime}", true, true);
        }
    }

    [HarmonyPatch(typeof(Walker), nameof(Walker.StopMoving))]
    public class WalkerStopMovingPatcher
    {
        static void Prefix(Walker __instance)
        {
            if (!Settings.Debug) return;
            var entityId = __instance.GetComponent<EntityComponent>()?.EntityId;
            DesyncDetecterService.Trace($"Walker {entityId} stopping movement");
        }
    }

    // Too many events; need a better place to track this
    //[HarmonyPatch(typeof(PathFollower), nameof(PathFollower.ReachedLastPathCorner))]
    //public class PathFollowerReachedLastPathCornerPatcher
    //{
    //    static void Prefix(PathFollower __instance)
    //    {
    //        if (!Settings.Debug) return;

    //        Vector3 lastCornerPos = Vector3.zero;
    //        if (__instance._pathCorners.Count > 0)
    //        {
    //            lastCornerPos = __instance._pathCorners[__instance._pathCorners.Count - 1].Position;
    //        }
    //        Vector3 transformPos = __instance._transform.position;
    //        DesyncDetecterService.Trace($"Checking if PathFollower has finished: " +
    //            $"lastCorner: {lastCornerPos}; transform: {transformPos}", true, true);
    //    }
    //}

    // Hashes the per-tick snapshot the water simulation actually consumes.
    // A divergence here precedes a "water map columns" divergence by one tick
    // and points at water source strength/contamination (e.g. frame-time based
    // strength modifiers) rather than the simulation itself.
    [HarmonyPatch(typeof(WaterSourceRegistry), nameof(WaterSourceRegistry.Tick))]
    public class WaterSourceRegistryTickPatcher
    {
        static void Postfix(WaterSourceRegistry __instance)
        {
            if (!Settings.Debug) return;
            var sources = __instance._threadSafeWaterSources;
            int hash = 13;
            foreach (var source in sources)
            {
                hash = (hash * 7) + BitConverter.SingleToInt32Bits(source.CurrentStrength);
                hash = (hash * 7) + BitConverter.SingleToInt32Bits(source.Contamination);
            }
            DesyncDetecterService.Trace($"Updating {sources.Count} water sources with hash {hash:X8}", true, true);
        }
    }
}
