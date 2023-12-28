using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BuilderPrioritySystem;
using Timberborn.Buildings;
using Timberborn.BuildingsBlocking;
using Timberborn.BuildingsUI;
using Timberborn.BuildingTools;
using Timberborn.Coordinates;
using Timberborn.DeconstructionSystemUI;
using Timberborn.DropdownSystem;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Planting;
using Timberborn.PrefabSystem;
using Timberborn.PrioritySystem;
using Timberborn.Workshops;
using Timberborn.WorkSystem;
using TimberModTest;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberModTest.Events
{

    [Serializable]
    abstract class BuildingDropdownEvent<Selector> : ReplayEvent where Selector : BaseComponent
    {
        public string itemID;
        public string entityID;

        public override void Replay(IReplayContext context)
        {
            if (entityID == null) return;

            
            var selector = GetComponent<Selector>(context, entityID);
            if (selector == null) return;
            SetValue(context, selector, itemID);
        }

        protected abstract void SetValue(IReplayContext context, Selector selector, string id);
    }

    class GatheringPrioritizedEvent : BuildingDropdownEvent<GatherablePrioritizer>
    {
        protected override void SetValue(IReplayContext context, GatherablePrioritizer prioritizer, string prefabName)
        {
            Gatherable gatherable = null;
            if (itemID != null)
            {
                gatherable = prioritizer.GetGatherable(prefabName);
                if (!gatherable)
                {
                    Plugin.LogWarning($"Could not find gatherable for prefab: {prefabName}");
                    return;
                }
            }
            prioritizer.PrioritizeGatherable(gatherable);
        }
    }

    [HarmonyPatch(typeof(GatherablePrioritizer), nameof(GatherablePrioritizer.PrioritizeGatherable))]
    class GatherablePrioritizerPatcher
    {
        static bool Prefix(GatherablePrioritizer __instance, Gatherable gatherable)
        {
            var name = gatherable?.GetComponentFast<Prefab>()?.PrefabName;
            string entityID = ReplayEvent.GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;

            Plugin.Log($"Prioritizing gathering for {entityID} to: {name}");

            ReplayService.RecordEvent(new GatheringPrioritizedEvent()
            {
                entityID = entityID,
                itemID = name,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    class ManufactoryRecipeSelectedEvent : BuildingDropdownEvent<Manufactory>
    {
        protected override void SetValue(IReplayContext context, Manufactory prioritizer, string itemID)
        {
            RecipeSpecification recipe = null;
            if (itemID != null)
            {
                recipe = context.GetSingleton<RecipeSpecificationService>()?.GetRecipe(itemID);
                if (recipe == null)
                {
                    Plugin.LogWarning($"Could not find recipe for id: {itemID}");
                    return;
                }
            }
            prioritizer.SetRecipe(recipe);
        }
    }

    [HarmonyPatch(typeof(Manufactory), nameof(Manufactory.SetRecipe))]
    class ManufactorySetRecipePatcher
    {
        static bool Prefix(Manufactory __instance, RecipeSpecification selectedRecipe)
        {
            var id = selectedRecipe?.Id;
            string entityID = ReplayEvent.GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Setting recipe for {entityID} to: {id}");

            ReplayService.RecordEvent(new ManufactoryRecipeSelectedEvent()
            {
                entityID = entityID,
                itemID = id,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    class PlantablePrioritizedEvent : BuildingDropdownEvent<PlantablePrioritizer>
    {
        protected override void SetValue(IReplayContext context, PlantablePrioritizer prioritizer, string itemID)
        {
            Plantable plantable = null;
            if (itemID != null)
            {
                var planterBuilding = prioritizer.GetComponentFast<PlanterBuilding>();
                plantable = planterBuilding?.AllowedPlantables.SingleOrDefault((plantable) => plantable.PrefabName == itemID);

                if (plantable == null)
                {
                    Plugin.LogWarning($"Could not find recipe for id: {itemID}");
                    return;
                }
            }
            prioritizer.PrioritizePlantable(plantable);
        }
    }

    [HarmonyPatch(typeof(PlantablePrioritizer), nameof(PlantablePrioritizer.PrioritizePlantable))]
    class PlantablePrioritizerPatcher
    {
        static bool Prefix(PlantablePrioritizer __instance, Plantable plantable)
        {
            var id = plantable?.PrefabName;
            string entityID = ReplayEvent.GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Setting prioritized plant for {entityID} to: {id}");

            ReplayService.RecordEvent(new PlantablePrioritizedEvent()
            {
                entityID = entityID,
                itemID = id,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    class SingleGoodAllowedEvent : BuildingDropdownEvent<SingleGoodAllower>
    {
        protected override void SetValue(IReplayContext context, SingleGoodAllower prioritizer, string itemID)
        {
            if (itemID == null)
            {
                prioritizer.Disallow();
            }
            else
            {
                prioritizer.Allow(itemID);
            }
        }
    }

    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Allow))]
    class SingleGoodAllowerAllowPatcher
    {
        static bool Prefix(SingleGoodAllower __instance, string goodId)
        {
            string entityID = ReplayEvent.GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Setting allowed good for {entityID} to: {goodId}");

            ReplayService.RecordEvent(new SingleGoodAllowedEvent()
            {
                entityID = entityID?.ToString(),
                itemID = goodId,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Disallow))]
    class SingleGoodAllowerDisallowPatcher
    {
        static bool Prefix(SingleGoodAllower __instance)
        {
            string entityID = ReplayEvent.GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Unsetting good for {entityID}");

            ReplayService.RecordEvent(new SingleGoodAllowedEvent()
            {
                entityID = entityID,
                itemID = null,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(DeleteBuildingFragment), nameof(DeleteBuildingFragment.DeleteBuilding))]
    class DeleteBuildingFragmentPatcher
    {
        static bool Prefix(DeleteBuildingFragment __instance)
        {
            if (!__instance.SelectedBuildingIsDeletable()) return true;
            if (!__instance._selectedBlockObject) return true;

            string entityID = ReplayEvent.GetEntityID(__instance._selectedBlockObject);

            ReplayService.RecordEvent(new BuildingsDeconstructedEvent()
            {
                entityIDs = new List<string>() { entityID },
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    class BuildingPausedChangedEvent : ReplayEvent
    {
        public string entityID;
        public bool wasPaused;

        public override void Replay(IReplayContext context)
        {
            var pausable = GetComponent<PausableBuilding>(context, entityID);
            if (!pausable) return;
            if (wasPaused) pausable.Pause();
            else pausable.Resume();
        }
    }

    [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.Pause))]
    class PausableBuildingPausePatcher
    {
        static bool Prefix(PausableBuilding __instance)
        {
            // Don't record if already paused
            if (__instance.Paused) return true;
            string entityID = ReplayEvent.GetEntityID(__instance);

            ReplayService.RecordEvent(new BuildingPausedChangedEvent()
            {
                entityID = entityID,
                wasPaused = true,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.Resume))]
    class PausableBuildingResumePatcher
    {
        static bool Prefix(PausableBuilding __instance)
        {
            // Don't record if already unpaused
            if (!__instance.Paused) return true;
            string entityID = ReplayEvent.GetEntityID(__instance);

            ReplayService.RecordEvent(new BuildingPausedChangedEvent()
            {
                entityID = entityID,
                wasPaused = false,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    abstract class PriorityChangedEvent<T> : ReplayEvent where T : BaseComponent, IPrioritizable
    {
        public string entityID;
        public Timberborn.PrioritySystem.Priority priority;

        public override void Replay(IReplayContext context)
        {
            var prioritizer = GetComponent<T>(context, entityID);
            if (!prioritizer) return;
            prioritizer.SetPriority(priority);
        }

        public static bool DoPrefix(BaseComponent __instance, Timberborn.PrioritySystem.Priority priority, Func<PriorityChangedEvent<T>> constructor)
        {
            string entityID = GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Setting priority for {entityID} to: {priority}");

            var evt = constructor();
            evt.entityID = entityID;
            evt.priority = priority;

            ReplayService.RecordEvent(evt);

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    class ConstructionPriorityChangedEvent : PriorityChangedEvent<BuilderPrioritizable>
    {
    }

    [HarmonyPatch(typeof(BuilderPrioritizable), nameof(BuilderPrioritizable.SetPriority))]
    class BuilderPrioritizableSetPriorityPatcher
    {
        static bool Prefix(BuilderPrioritizable __instance, Timberborn.PrioritySystem.Priority priority)
        {
            if (__instance.Priority == priority) return true;
            return PriorityChangedEvent<BuilderPrioritizable>
                .DoPrefix(__instance, priority, () => new ConstructionPriorityChangedEvent());
        }
    }
    
    class WorkplacePriorityChangedEvent : PriorityChangedEvent<WorkplacePriority>
    {
    }

    [HarmonyPatch(typeof(WorkplacePriority), nameof(WorkplacePriority.SetPriority))]
    class WorkplacePrioritySetPriorityPatcher
    {
        static bool Prefix(WorkplacePriority __instance, Timberborn.PrioritySystem.Priority priority)
        {
            if (__instance.Priority == priority) return true;
            return PriorityChangedEvent<WorkplacePriority>
                .DoPrefix(__instance, priority, () => new WorkplacePriorityChangedEvent());
        }
    }

    class WorkplaceDesiredWorkersChangedEvent : ReplayEvent
    {
        public string entityID;
        public bool increased;

        public override void Replay(IReplayContext context)
        {
            var workplace = GetComponent<Workplace>(context, entityID);
            if (!workplace) return;
            if (increased) workplace.IncreaseDesiredWorkers();
            else workplace.DecreaseDesiredWorkers();
        }

        public static bool DoPrefix(Workplace __instance, bool increased)
        {
            string entityID = GetEntityID(__instance);
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;

            // Ignore if we're already at max/min workers
            if (increased && __instance.DesiredWorkers >= __instance._workplaceSpecification.MaxWorkers) return true;
            if (!increased && __instance.DesiredWorkers <= 1) return true;

            Plugin.Log($"Changing desired workers for {entityID} - increasing: {increased}");

            ReplayService.RecordEvent(new WorkplaceDesiredWorkersChangedEvent()
            {
                entityID = entityID,
                increased = increased,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(Workplace), nameof(Workplace.IncreaseDesiredWorkers))]
    class WorkplaceIncreaseDesiredWorkersPatcher
    {
        static bool Prefix(Workplace __instance)
        {
            return WorkplaceDesiredWorkersChangedEvent.DoPrefix(__instance, true);
        }
    }

    [HarmonyPatch(typeof(Workplace), nameof(Workplace.DecreaseDesiredWorkers))]
    class WorkplaceDecreaseDesiredWorkersPatcher
    {
        static bool Prefix(Workplace __instance)
        {
            return WorkplaceDesiredWorkersChangedEvent.DoPrefix(__instance, false);
        }
    }

    // For debuggin only - to figure out where dropdowns are being set
    [HarmonyPatch(typeof(Dropdown), nameof(Dropdown.SetAndUpdate))]
    class DropdownPatcher
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

    // For debuggin only - to figure out where dropdowns are being set
    [HarmonyPatch(typeof(Dropdown), nameof(Dropdown.SetItems))]
    class DropdownProviderPatcher
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
