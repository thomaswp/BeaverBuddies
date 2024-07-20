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

namespace BeaverBuddies.DesyncDetecter
{

    [HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.MarkSpots))]
    class NRPMarkSpotsPatcher
    {
        private static int lastCount;
        public static void Prefix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!EventIO.Config.Debug) return;
            if (!ReplayService.IsLoaded) return;
            var key = ReproducibleKey.Create(reproducible);
            lastCount = __instance._potentialSpots.ContainsKey(key) ? __instance._potentialSpots[key].Count : 0;
            DesyncDetecterService.Trace($"Marking spots for   {reproducible.Id} at {reproducible.GetComponentFast<BlockObject>().Coordinates} ({reproducible.GetComponentFast<EntityComponent>().EntityId})");
        }

        public static void Postfix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!EventIO.Config.Debug) return;
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
            if (!EventIO.Config.Debug) return;
            if (!ReplayService.IsLoaded) return;
            var key = ReproducibleKey.Create(reproducible);
            lastCount = __instance._potentialSpots.ContainsKey(key) ? __instance._potentialSpots[key].Count : 0; if (!ReplayService.IsLoaded) return;
            DesyncDetecterService.Trace($"Unmarking spots for   {reproducible.Id} at {reproducible.GetComponentFast<BlockObject>().Coordinates} ({reproducible.GetComponentFast<EntityComponent>().EntityId})");
        }

        static void Postfix(NaturalResourceReproducer __instance, Reproducible reproducible)
        {
            if (!EventIO.Config.Debug) return;
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
        public static void Postfix(SpawnValidationService __instance, bool __result, Vector3Int coordinates, Blocks blocks, string resourcePrefabName)
        {
            if (!EventIO.Config.Debug) return;
            DesyncDetecterService.Trace($"Trying to spawn {resourcePrefabName} at {coordinates}: {__result}\n" +
                $"IsSuitableTerrain: {__instance.IsSuitableTerrain(coordinates)}\n" +
                $"SpotIsValid: {__instance.SpotIsValid(coordinates, resourcePrefabName)}\n" +
                $"IsUnobstructed: {__instance.IsUnobstructed(coordinates, blocks)}");
        }
    }

    [HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.SpawnNewResources))]
    public class NRRPatcher
    {
        public static void Prefix(NaturalResourceReproducer __instance)
        {
            if (!EventIO.Config.Debug) return;
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
            if (!EventIO.Config.Debug) return;
            string entityID = __instance.GetComponentFast<EntityComponent>().EntityId.ToString();
            if (destination is PositionDestination)
            {
                DesyncDetecterService.Trace($"{entityID} going to: " +
                    $"{((PositionDestination)destination)?.Destination}");
            }
            else if (destination is AccessibleDestination)
            {
                var accessible = ((AccessibleDestination)destination).Accessible;
                // Manually check since MonoBehavior doesn't support null conditional operator
                if (accessible == null) return;
                DesyncDetecterService.Trace($"{entityID} going to: " +
                    $"{accessible?.GameObjectFast?.name}");
            }
        }
    }

    [HarmonyPatch(typeof(WateredNaturalResource), nameof(WateredNaturalResource.StartDryingOut))]
    public class WateredNaturalResourceStartDryingOutPatcher
    {
        public static void Prefix(WateredNaturalResource __instance)
        {
            if (!EventIO.Config.Debug) return;
            var id = __instance.GetComponentFast<EntityComponent>().EntityId;
            var isDead = __instance._livingNaturalResource.IsDead;
            var time = ((TimeTrigger)__instance._timeTrigger)._delayLeftInDays;
            DesyncDetecterService.Trace(
                $"WateredNaturalResource {id} [dead={isDead}] starting to dry out; " +
                $"trigger delay = {time}");
        }
    }
}
