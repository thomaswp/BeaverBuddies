using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Timberborn.Coordinates;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using System.Linq;
using Timberborn.Forestry;
using Timberborn.DropdownSystem;
using UnityEngine.UIElements;
using Timberborn.TimeSystem;
using Timberborn.Gathering;
using Timberborn.PrefabSystem;
using Timberborn.EntitySystem;
using Timberborn.Workshops;
using Timberborn.Goods;
using Timberborn.InventorySystem;

namespace TimberModTest
{
    public interface IReplayContext
    {
        T GetSingleton<T>();
    }

    public abstract class ReplayEvent : IComparable<ReplayEvent>
    {
        public float timeInFixedSecs;
        public int ticksSinceLoad;
        public int? randomS0Before;

        public string type => GetType().Name;

        //public ReplayEvent(float timeInFixedSecs)
        //{
        //    this.timeInFixedSecs = timeInFixedSecs;
        //}

        public int CompareTo(ReplayEvent other)
        {
            if (other == null)
                return 1;
            //return timeInFixedSecs.CompareTo(other.timeInFixedSecs);
            return ticksSinceLoad.CompareTo(other.ticksSinceLoad);
        }
        
        //public void SetTime(TickWathcerService tickWathcerService)
        //{
        //    if (tickWathcerService == null) return;
        //    timeInFixedSecs = tickWathcerService.TotalTimeInFixedSecons;
        //    ticksSinceLoad = tickWathcerService.TicksSinceLoad;
        //}

        public abstract void Replay(IReplayContext context);

        public override string ToString()
        {
            return type;
        }
    }

    [Serializable]
    public class RandomStateSetEvent : ReplayEvent
    {
        public int seed;

        public override void Replay(IReplayContext context)
        {
            UnityEngine.Random.InitState(seed);
            Plugin.Log($"Seeting seed to {seed}; s0 = {UnityEngine.Random.state.s0}");
        }

        public static RandomStateSetEvent CreateAndExecute()
        {
            int seed = UnityEngine.Random.RandomRangeInt(int.MinValue, int.MaxValue);
            RandomStateSetEvent message = new RandomStateSetEvent()
            {
                seed = seed
            };
            // TODO: Not certain if this is the right time, or if it should be enqueued
            message.Replay(null);
            return message;
        }
    }

    [Serializable]
    public class SpeedSetEvent : ReplayEvent
    {
        public int speed;

        public override void Replay(IReplayContext context)
        {
            SpeedManager sm = context.GetSingleton<SpeedManager>();
            Plugin.Log($"Event: Changing speed from {sm.CurrentSpeed} to {speed}");
            if (sm.CurrentSpeed != speed) sm.ChangeSpeed(speed);

            ReplayService replayService = context.GetSingleton<ReplayService>();
            if (speed != replayService.TargetSpeed)
            {
                Plugin.Log($"Event: Changing target speed from {replayService.TargetSpeed} to {speed}");
                replayService.SetTargetSpeed(speed);
            }
        }
    }


    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.ChangeSpeed))]
    public class SpeedChangePatcher
    {
        private static bool silently = false;

        public static void SetSpeedSilently(SpeedManager speedManager, int speed)
        {
            silently = true;
            speedManager.ChangeSpeed(speed);
            silently = false;
        }

        static bool Prefix(SpeedManager __instance, ref int speed)
        {
            // No need to log speed changes to current speed
            if (__instance.CurrentSpeed == speed) return true;
            // Also don't log if we're silent
            if (silently) return true;

            ReplayService.RecordEvent(new SpeedSetEvent()
            {
                speed = speed
            });

            if (EventIO.ShouldPlayPatchedEvents)
            {
                // If this will actually change the speed, make sure
                // we shouldn't pause instead.
                if (EventIO.ShouldPauseTicking) speed = 0;
                return true;
            }
            return false;
        }
    }


    [Serializable]
    public class BuildingPlacedEvent : ReplayEvent
    {
        public string prefabName;
        public Vector3Int coordinates;
        public Orientation orientation;

        //public BuildingPlacedEvent(float timeInFixedSecs, string prefab, Vector3Int coordinates, Orientation orientation) : base(timeInFixedSecs)
        //{
        //    this.prefab = prefab;
        //    this.coordinates = coordinates;
        //    this.orientation = orientation;
        //}

        public override void Replay(IReplayContext context)
        {
            var buildingPrefab = context.GetSingleton<BuildingService>().GetBuildingPrefab(prefabName);
            var blockObject = buildingPrefab.GetComponentFast<BlockObject>();
            var placer = context.GetSingleton<BlockObjectPlacerService>().GetMatchingPlacer(blockObject);
            placer.Place(blockObject, coordinates, orientation);
        }
    }


    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.Place))]
    public class PlacePatcher
    {
        static bool Prefix(BlockObject prefab, Vector3Int coordinates, Orientation orientation)
        {
            Plugin.Log($"Placing {prefab.name}, {coordinates}, {orientation}");

            //System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            //Plugin.Log(t.ToString());

            ReplayService.RecordEvent(new BuildingPlacedEvent()
            {
                prefabName = prefab.name,
                coordinates = coordinates,
                orientation = orientation,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [Serializable]
    public class PlantingAreaMarkedEvent : ReplayEvent
    {
        public List<Vector3Int> inputBlocks;
        public Ray ray;
        public string prefabName;

        public override void Replay(IReplayContext context)
        {
            var plantingService = context.GetSingleton<PlantingSelectionService>();
            plantingService.MarkArea(inputBlocks, ray, prefabName);
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.MarkArea))]
    public class PlantingAreaMarkedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, string prefabName)
        {
            Plugin.Log($"Planting {inputBlocks.Count()} of {prefabName}");

            //System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            //Plugin.Log(t.ToString());

            ReplayService.RecordEvent(new PlantingAreaMarkedEvent()
            {
                prefabName = prefabName,
                ray = ray,
                inputBlocks = new List<Vector3Int>(inputBlocks)
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [Serializable]
    public class TreeCuttingAreaEvent : ReplayEvent
    {
        public List<Vector3Int> coordinates;
        public bool wasAdded;

        public override void Replay(IReplayContext context)
        {
            // TODO: Looks like this doesn't work until the receiver
            // has at least opened the tool tray. Need to test more.
            var treeService = context.GetSingleton<TreeCuttingArea>();
            if (wasAdded)
            {
                treeService.AddCoordinates(coordinates);
            }
            else
            {
                treeService.RemoveCoordinates(coordinates);
            }
        }
    }

    // TODO: These events seem to only replay successfully if the
    // tool is open...
    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.AddCoordinates))]
    public class TreeCuttingAreaAddedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            Plugin.Log($"Adding tree planting coordinate {coordinates.Count()}");

            ReplayService.RecordEvent(new TreeCuttingAreaEvent()
            {
                coordinates = new List<Vector3Int>(coordinates),
                wasAdded = true,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.RemoveCoordinates))]
    public class TreeCuttingAreaRemovedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            Plugin.Log($"Removing tree planting coordinate {coordinates.Count()}");

            ReplayService.RecordEvent(new TreeCuttingAreaEvent()
            {
                coordinates = new List<Vector3Int>(coordinates),
                wasAdded = false,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }
}
