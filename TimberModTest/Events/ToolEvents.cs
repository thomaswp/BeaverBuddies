using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.PlantingUI;
using UnityEngine;

namespace TimberModTest.Events
{

    [Serializable]
    class BuildingPlacedEvent : ReplayEvent
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
    class PlacePatcher
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

    class BuildingsDeconstructedEvent : ReplayEvent
    {
        public List<string> entityIDs = new List<string>();

        public override void Replay(IReplayContext context)
        {
            var entityService = context.GetSingleton<EntityService>();
            foreach (string entityID in entityIDs)
            {
                var entity = GetEntityComponent(context, entityID);
                if (entity == null) continue;
                entityService.Delete(entity);
            }
        }
    }

    [HarmonyPatch(typeof(BlockObjectDeletionTool<Building>), nameof(BlockObjectDeletionTool<Building>.DeleteBlockObjects))]
    class BuildingDeconstructionPatcher
    {
        static bool Prefix(BlockObjectDeletionTool<Building> __instance)
        {
            // TODO: If this does work, it may affect other deletions too :(
            List<string> entityIDs = __instance._temporaryBlockObjects
                    .Select(ReplayEvent.GetEntityID)
                    .ToList();
            Plugin.Log($"Deconstructing: {string.Join(", ", entityIDs)}");

            ReplayService.RecordEvent(new BuildingsDeconstructedEvent()
            {
                entityIDs = entityIDs,
            });

            if (!EventIO.ShouldPlayPatchedEvents)
            {
                // If we cancel the event, clean up the tool
                __instance._temporaryBlockObjects.Clear();
            }

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [Serializable]
    class PlantingAreaMarkedEvent : ReplayEvent
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
    class PlantingAreaMarkedPatcher
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
    class TreeCuttingAreaEvent : ReplayEvent
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
    class TreeCuttingAreaAddedPatcher
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
    class TreeCuttingAreaRemovedPatcher
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
