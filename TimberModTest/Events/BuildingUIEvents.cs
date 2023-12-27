using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
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
using Timberborn.Workshops;
using TimberModTest;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberModTest.Events
{

    [Serializable]
    abstract class BuildingDropdownEvent<Selector> : ReplayEvent
    {
        public string itemID;
        public string entityID;

        public override void Replay(IReplayContext context)
        {
            if (entityID == null) return;

            var registry = context.GetSingleton<EntityRegistry>();
            if (!Guid.TryParse(entityID, out var guid))
            {
                Plugin.LogWarning("Invalid GUID: " + entityID);
                return;
            }
            var entity = registry.GetEntity(guid);
            if (entity == null)
            {
                Plugin.LogWarning($"Could not find entity with ID {entityID}");
                return;
            }
            var prioritizer = entity.GetComponentFast<Selector>();
            if (prioritizer == null)
            {
                Plugin.LogWarning($"Could not find selector for entity with ID {entityID}");
                return;
            }
            SetValue(context, prioritizer, itemID);
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
            var entityID = __instance.GetComponentFast<EntityComponent>()?.EntityId;
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;

            Plugin.Log($"Prioritizing gathering for {entityID} to: {name}");

            ReplayService.RecordEvent(new GatheringPrioritizedEvent()
            {
                entityID = entityID?.ToString(),
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
            var entityID = __instance.GetComponentFast<EntityComponent>()?.EntityId;
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Setting recipe for {entityID} to: {id}");

            ReplayService.RecordEvent(new ManufactoryRecipeSelectedEvent()
            {
                entityID = entityID?.ToString(),
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
            var entityID = __instance.GetComponentFast<EntityComponent>()?.EntityId;
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Setting prioritized plant for {entityID} to: {id}");

            ReplayService.RecordEvent(new PlantablePrioritizedEvent()
            {
                entityID = entityID?.ToString(),
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
            var entityID = __instance.GetComponentFast<EntityComponent>()?.EntityId;
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
            var entityID = __instance.GetComponentFast<EntityComponent>()?.EntityId;
            // Play events directly if they're happening to a non-entity (e.g. prefab);
            if (entityID == null) return true;
            Plugin.Log($"Unsetting good for {entityID}");

            ReplayService.RecordEvent(new SingleGoodAllowedEvent()
            {
                entityID = entityID?.ToString(),
                itemID = null,
            });

            return EventIO.ShouldPlayPatchedEvents;
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
