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
using Timberborn.DemolishingUI;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.PlantingUI;
using Timberborn.ScienceSystem;
using Timberborn.ToolSystem;
using Timberborn.WorkSystem;
using Timberborn.WorkSystemUI;
using UnityEngine;

namespace TimberModTest.Events
{

    [Serializable]
    class BuildingPlacedEvent : ReplayEvent
    {
        public string prefabName;
        public Vector3Int coordinates;
        public Orientation orientation;
        public FlipMode flipMode;

        public override void Replay(IReplayContext context)
        {
            var buildingPrefab = GetBuilding(context, prefabName);
            var blockObject = buildingPrefab.GetComponentFast<BlockObject>();
            var placer = context.GetSingleton<BlockObjectPlacerService>().GetMatchingPlacer(blockObject);
            Placement placement = new Placement(coordinates, orientation, flipMode);
            placer.Place(blockObject, placement);
        }

        public override string ToActionString()
        {
            return $"Placing {prefabName}, {coordinates}, {orientation}, {flipMode}";
        }
    }


    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.Place))]
    class PlacePatcher
    {
        static bool Prefix(BlockObject prefab, Placement placement)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                string prefabName = ReplayEvent.GetBuildingName(prefab);

                return new BuildingPlacedEvent()
                {
                    prefabName = prefabName,
                    coordinates = placement.Coordinates,
                    orientation = placement.Orientation,
                    flipMode = placement.FlipMode,
                };
            });
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

        public override string ToActionString()
        {
            return $"Deconstructing: {string.Join(", ", entityIDs)}";
        }
    }

    [HarmonyPatch(typeof(BlockObjectDeletionTool<Building>), nameof(BlockObjectDeletionTool<Building>.DeleteBlockObjects))]
    class BuildingDeconstructionPatcher
    {
        static bool Prefix(BlockObjectDeletionTool<Building> __instance)
        {
            bool result = ReplayEvent.DoPrefix(() =>
            {
                // TODO: If this does work, it may affect other deletions too :(
                List<string> entityIDs = __instance._temporaryBlockObjects
                        .Select(ReplayEvent.GetEntityID)
                        .ToList();

                return new BuildingsDeconstructedEvent()
                {
                    entityIDs = entityIDs,
                };
            });

            if (!result)
            {
                // If we cancel the event, clean up the tool
                __instance._temporaryBlockObjects.Clear();
            }

            return result;
        }
    }

    [Serializable]
    class PlantingAreaMarkedEvent : ReplayEvent
    {
        public List<Vector3Int> inputBlocks;
        public Ray ray;
        public string prefabName;

        public const string UNMARK = "Unmark";

        public override void Replay(IReplayContext context)
        {
            var plantingService = context.GetSingleton<PlantingSelectionService>();
            if (prefabName == UNMARK)
            {
                plantingService.UnmarkArea(inputBlocks, ray);
            }
            else
            {
                plantingService.MarkArea(inputBlocks, ray, prefabName);
            }
        }

        public override string ToActionString()
        {
            return $"Planting {inputBlocks.Count()} of {prefabName}";
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.MarkArea))]
    class PlantingAreaMarkedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, string prefabName)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new PlantingAreaMarkedEvent()
                {
                    prefabName = prefabName,
                    ray = ray,
                    inputBlocks = new List<Vector3Int>(inputBlocks)
                };
            });
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.UnmarkArea))]
    class PlantingAreaUnmarkedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new PlantingAreaMarkedEvent()
                {
                    prefabName = PlantingAreaMarkedEvent.UNMARK,
                    ray = ray,
                    inputBlocks = new List<Vector3Int>(inputBlocks)
                };
            });
        }
    }

    [Serializable]
    class ClearResourcesMarkedEvent : ReplayEvent
    {
        public List<Vector3Int> blocks;
        public Ray ray;
        public bool markForDemolition;

        public override void Replay(IReplayContext context)
        {
            var demolitionService = context.GetSingleton<DemolishableSelectionService>();
            if (markForDemolition)
            {
                demolitionService.MarkDemolishablesInArea(blocks, ray);
            }
            else
            {
                demolitionService.UnmarkDemolishablesInArea(blocks, ray);
            }
        }

        public override string ToActionString()
        {
            return $"Setting {blocks.Count()} as marked: {markForDemolition}";
        }

        public static bool DoPrefix(IEnumerable<Vector3Int> blocks, Ray ray, bool markForDemolition)
        {
            return DoPrefix(() =>
            {
                return new ClearResourcesMarkedEvent()
                {
                    markForDemolition = markForDemolition,
                    ray = ray,
                    blocks = new List<Vector3Int>(blocks)
                };
            });
        }
    }

    [HarmonyPatch(typeof(DemolishableSelectionService), nameof(DemolishableSelectionService.MarkDemolishablesInArea))]
    class DemolishableSelectionServiceMarkPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> blocks, Ray ray)
        {
            return ClearResourcesMarkedEvent.DoPrefix(blocks, ray, true);
        }
    }

    [HarmonyPatch(typeof(DemolishableSelectionService), nameof(DemolishableSelectionService.UnmarkDemolishablesInArea))]
    class DemolishableSelectionServiceUnmarkPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> blocks, Ray ray)
        {
            return ClearResourcesMarkedEvent.DoPrefix(blocks, ray, false);
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

        public override string ToActionString()
        {
            string verb = wasAdded ? "Added" : "Removed";
            return $"{verb} tree planting coordinate {coordinates.Count()}";
        }
    }

    // TODO: These events seem to only replay successfully if the
    // tool is open...
    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.AddCoordinates))]
    class TreeCuttingAreaAddedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new TreeCuttingAreaEvent()
                {
                    coordinates = new List<Vector3Int>(coordinates),
                    wasAdded = true,
                };
            });
        }
    }

    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.RemoveCoordinates))]
    class TreeCuttingAreaRemovedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new TreeCuttingAreaEvent()
                {
                    coordinates = new List<Vector3Int>(coordinates),
                    wasAdded = false,
                };
            });
        }
    }

    [Serializable]
    class BuildingUnlockedEvent : ReplayEvent
    {
        public string buildingName;

        public override void Replay(IReplayContext context)
        {
            var building = GetBuilding(context, buildingName);
            if (building == null) return;
            context.GetSingleton<BuildingUnlockingService>().Unlock(building);

            var toolButtonService = context.GetSingleton<ToolButtonService>();

            foreach (ToolButton toolButton in toolButtonService.ToolButtons)
            {
                Tool tool = toolButton.Tool;
                BlockObjectTool blockObjectTool = tool as BlockObjectTool;
                if (blockObjectTool == null)
                {
                    continue;
                }
                Building toolBuilding = blockObjectTool.Prefab.GetComponentFast<Building>();
                if (toolBuilding == building)
                {
                    Plugin.Log("Unlocking tool for building: " + buildingName);
                    // TODO: Need to test - I think this is equivalent to clearing the lock
                    // but not sure it'll update the UI
                    blockObjectTool.Locker = null;
                }
            }
        }

        public override string ToActionString()
        {
            return $"Unlocking building: {buildingName}";
        }
    }

    [HarmonyPatch(typeof(BuildingUnlockingService), nameof(BuildingUnlockingService.Unlock))]
    class BuildingUnlockingServiceUnlockPatcher
    {
        static bool Prefix(Building building)
        {
            //Plugin.LogWarning("science again!");
            //Plugin.LogStackTrace();
            return ReplayEvent.DoPrefix(() =>
            {
                return new BuildingUnlockedEvent()
                {
                    buildingName = building.name,
                };
            });
        }
    }

    [Serializable]
    class WorkingHoursChangedEvent : ReplayEvent
    {
        public int hours;

        public override void Replay(IReplayContext context)
        {
            var panel = context.GetSingleton<WorkingHoursPanel>();
            panel._hours = hours;
            panel.OnHoursChanged();
        }

        public override string ToActionString()
        {
            return $"Setting working hours: {hours}";
        }
    }

    [HarmonyPatch(typeof(WorkingHoursPanel), nameof(WorkingHoursPanel.OnHoursChanged))]
    class WorkingHoursPanelOnHoursChangedPatcher
    {
        static bool Prefix(WorkingHoursPanel __instance)
        {
            bool value = ReplayEvent.DoPrefix(() =>
            {
                return new WorkingHoursChangedEvent()
                {
                    hours = __instance._hours,
                };
            });

            // Update the title if we're actually calling this event
            if (!value) __instance.UpdateTitle();
            return value;
        }
    }
}
