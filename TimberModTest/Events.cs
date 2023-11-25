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
            Plugin.Log($"Seeting seed to {seed}");
            UnityEngine.Random.InitState(seed);
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

            return EventIO.ShouldPlayRecordedEvents;
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

            return EventIO.ShouldPlayRecordedEvents;
        }
    }

    [Serializable]
    public class TreeCuttingAreaEvent : ReplayEvent
    {
        public List<Vector3Int> coordinates;
        public bool wasAdded;

        public override void Replay(IReplayContext context)
        {
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

            return EventIO.ShouldPlayRecordedEvents;
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

            return EventIO.ShouldPlayRecordedEvents;
        }
    }

    // TODO: Probably not the right way to go about this
    // need to find the _dropdownProvider and see what it does and
    // replicate that. But could use this to find all of them
    // GatheringUI, WorkshopsUI, PlantingUI have PrioritizerDropdowns
    // which are places to start. Likely others.
    [HarmonyPatch(typeof(Dropdown), nameof(Dropdown.SetAndUpdate))]
    public class DropdownPatcher
    {
        static bool Prefix(Dropdown __instance, string newValue)
        {
            Plugin.Log($"Dropdown selected {newValue}");
            Plugin.Log(__instance.name + "," + __instance.fullTypeName);
            //Plugin.Log(new System.Diagnostics.StackTrace().ToString());

            // TODO: For reader, return false
            return true;
        }
    }

    [HarmonyPatch(typeof(Dropdown), nameof(Dropdown.SetItems))]
    public class DropdownProviderPatcher
    {
        static bool Prefix(Dropdown __instance, IDropdownProvider dropdownProvider, Func<string, VisualElement> elementGetter)
        {
            Plugin.Log($"Dropdown set {dropdownProvider} {dropdownProvider.GetType().FullName}");
            //Plugin.Log(new System.Diagnostics.StackTrace().ToString());

            // TODO: For reader, return false
            return true;
        }
    }
}
