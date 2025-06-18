using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using static Timberborn.NaturalResourcesReproduction.NaturalResourceReproducer;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.NaturalResourcesReproduction;
using Timberborn.TimeSystem;
using Timberborn.NaturalResources;
using UnityEngine;
using Timberborn.WalkingSystem;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.SoilMoistureSystem;
using BeaverBuddies.IO;
using Timberborn.WaterSystem;
using Timberborn.TickSystem;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.ReservableSystem;

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
            DesyncDetecterService.Trace($"Marking spots for   {reproducible.Id} at {reproducible.GetComponentFast<BlockObject>().Coordinates} ({reproducible.GetComponentFast<EntityComponent>().EntityId})");
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
            DesyncDetecterService.Trace($"Unmarking spots for   {reproducible.Id} at {reproducible.GetComponentFast<BlockObject>().Coordinates} ({reproducible.GetComponentFast<EntityComponent>().EntityId})");
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
        public static void Postfix(SpawnValidationService __instance, bool __result, Vector3Int coordinates, BlockObjectSpec blockObjectSpec, string resourcePrefabName)
        {
            if (!Settings.Debug) return;
            DesyncDetecterService.Trace($"Trying to spawn {resourcePrefabName} at {coordinates}: {__result}\n" +
                $"IsSuitableTerrain: {__instance.IsSuitableTerrain(coordinates)}\n" +
                $"SpotIsValid: {__instance.SpotIsValid(coordinates, resourcePrefabName)}\n" +
                $"IsUnobstructed: {__instance.IsUnobstructed(coordinates, resourcePrefabName)}");
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
            string entityID = __instance.GetComponentFast<EntityComponent>().EntityId.ToString();
            string destinationString = GetDestinationString(destination);
            DesyncDetecterService.Trace($"{entityID} going to: {destinationString}");
        }

        public static string GetDestinationString(IDestination destination)
        {
            string destinationString = null;
            if (destination is PositionDestination)
            {
                destinationString = ((PositionDestination)destination)?.Destination.ToString();
            }
            else if (destination is AccessibleDestination)
            {
                var accessible = ((AccessibleDestination)destination).Accessible;
                // Manually check since MonoBehavior doesn't support null conditional operator
                if (accessible != null) destinationString = accessible?.GameObjectFast?.name;
            }
            if (destinationString == null) destinationString = destination?.GetType().Name;
            return destinationString;
        }
    }

    // In theory this is deterministic based on the model's GUID, so it shouldn't diverge
    [HarmonyPatch(typeof(NaturalResourceModelRandomizer), nameof(NaturalResourceModelRandomizer.RandomizeDiameterScale))]
    public class NaturalResourceModelRandomizerPatcher
    {
        public static void Postfix(NaturalResourceModelRandomizer __instance)
        {
            if (!Settings.Debug) return;
            var id = __instance.GetComponentFast<EntityComponent>().EntityId;
            DesyncDetecterService.Trace($"NaturalResourceModelRandomizer {id} randomizing diameter scale to {__instance.DiameterScale}");
        }
    }

    [HarmonyPatch(typeof(WalkToReservableExecutor), nameof(WalkToReservableExecutor.Launch))]
    public class WalkToReservableExecutorPatcher
    {
        public static void Prefix(WalkToReservableExecutor __instance, ReservableReacher reservableReacher)
        {
            if (!Settings.Debug) return;
            var id = __instance.GetComponentFast<EntityComponent>().EntityId;
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
            var id = __instance.GetComponentFast<EntityComponent>().EntityId;
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

    [HarmonyPatch(typeof(SoilMoistureMap), nameof(SoilMoistureMap.UpdateMoistureLevels))]
    public class SoilMoistureMapSetMoistureLevelPatcher
    {
        public static void Postfix(SoilMoistureMap __instance)
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

            var levels = __instance._soilMoistureSimulator._moistureLevels;
            int hash = 13;
            foreach (var level in levels)
            {
                hash = (hash * 7) + BitConverter.SingleToInt32Bits(level);
            }
            DesyncDetecterService.Trace($"Updating moisture levels with hash {hash:X8}");
        }
    }

    [HarmonyPatch(typeof(ThreadSafeWaterMap), nameof(ThreadSafeWaterMap.UpdateData))]
    public class ThreadSafeWaterMapUpdateDataPatcher
    {
        public static void Postfix(ThreadSafeWaterMap __instance)
        {
            if (!Settings.Debug) return;

            var columns = __instance._waterColumns;
            int hash = 13;
            foreach (var level in columns)
            {
                hash = (hash * 7) + GetHashCode(level);
            }
            DesyncDetecterService.Trace($"Updating water map columns with hash {hash:X8}");
            
            hash = 13;
            var counts = __instance._columnCounts;
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
}
