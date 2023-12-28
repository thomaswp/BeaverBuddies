using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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
using Timberborn.Emptying;
using Timberborn.EntitySystem;
using Timberborn.Fields;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Planting;
using Timberborn.PrefabSystem;
using Timberborn.PrioritySystem;
using Timberborn.StockpilePrioritySystem;
using Timberborn.StockpilePriorityUISystem;
using Timberborn.WaterBuildings;
using Timberborn.Workshops;
using Timberborn.WorkSystem;
using TimberModTest;
using Unity.IO.LowLevel.Unsafe;
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

        public override string ToActionString()
        {
            return $"Prioritizing gathering for {entityID} to: {itemID}";
        }
    }

    [HarmonyPatch(typeof(GatherablePrioritizer), nameof(GatherablePrioritizer.PrioritizeGatherable))]
    class GatherablePrioritizerPatcher
    {
        static bool Prefix(GatherablePrioritizer __instance, Gatherable gatherable)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                var name = gatherable?.GetComponentFast<Prefab>()?.PrefabName;
                return new GatheringPrioritizedEvent()
                {
                    entityID = entityID,
                    itemID = name,
                };
            });
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

        public override string ToActionString()
        {
            return $"Setting recipe for {entityID} to: {itemID}";
        }
    }

    [HarmonyPatch(typeof(Manufactory), nameof(Manufactory.SetRecipe))]
    class ManufactorySetRecipePatcher
    {
        static bool Prefix(Manufactory __instance, RecipeSpecification selectedRecipe)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                var id = selectedRecipe?.Id;
                return new ManufactoryRecipeSelectedEvent()
                {
                    entityID = entityID,
                    itemID = id,
                };
            });
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

        public override string ToActionString()
        {
            return $"Setting prioritized plant for {entityID} to: {itemID}";
        }
    }

    [HarmonyPatch(typeof(PlantablePrioritizer), nameof(PlantablePrioritizer.PrioritizePlantable))]
    class PlantablePrioritizerPatcher
    {
        static bool Prefix(PlantablePrioritizer __instance, Plantable plantable)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                var id = plantable?.PrefabName;

                return new PlantablePrioritizedEvent()
                {
                    entityID = entityID,
                    itemID = id,
                };
            });
        }
    }

    class FarmHousePrioritizePlantingChangedEvent : ReplayEvent
    {
        public string entityID;
        public bool prioritizePlanting;

        public override void Replay(IReplayContext context)
        {
            var farmhouse = GetComponent<FarmHouse>(context, entityID);
            if (farmhouse == null) return;
            if (prioritizePlanting)
            {
                farmhouse.PrioritizePlanting();
            }
            else
            {
                farmhouse.UnprioritizePlanting();
            }
        }

        public override string ToActionString()
        {
            return $"Setting prioritize planting for {entityID} to: {prioritizePlanting}";
        }

        public static bool DoPrefix(FarmHouse farmHouse, bool prioritizePlanting)
        {
            if (farmHouse.PlantingPrioritized == prioritizePlanting) return true;
            return DoEntityPrefix(farmHouse, entityID =>
            {
                return new FarmHousePrioritizePlantingChangedEvent()
                {
                    entityID = entityID,
                    prioritizePlanting = prioritizePlanting,
                };
            });
        }
    }

    [HarmonyPatch(typeof(FarmHouse), nameof(FarmHouse.PrioritizePlanting))]
    class FarmHousePrioritizePlantingPatcher
    {
        public static bool Prefix(FarmHouse __instance)
        {
            return FarmHousePrioritizePlantingChangedEvent.DoPrefix(__instance, true);
        }
    }

    [HarmonyPatch(typeof(FarmHouse), nameof(FarmHouse.UnprioritizePlanting))]
    class FarmHouseUnprioritizePlantingPatcher
    {
        public static bool Prefix(FarmHouse __instance)
        {
            return FarmHousePrioritizePlantingChangedEvent.DoPrefix(__instance, false);
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

        public override string ToActionString()
        {
            return $"Setting allowed good for {entityID} to: {itemID}";
        }
    }

    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Allow))]
    class SingleGoodAllowerAllowPatcher
    {
        static bool Prefix(SingleGoodAllower __instance, string goodId)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new SingleGoodAllowedEvent()
                {
                    entityID = entityID,
                    itemID = goodId,
                };
            });
        }
    }

    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Disallow))]
    class SingleGoodAllowerDisallowPatcher
    {
        static bool Prefix(SingleGoodAllower __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new SingleGoodAllowedEvent()
                {
                    entityID = entityID,
                    itemID = null,
                };
            });
        }
    }

    [HarmonyPatch(typeof(DeleteBuildingFragment), nameof(DeleteBuildingFragment.DeleteBuilding))]
    class DeleteBuildingFragmentPatcher
    {
        static bool Prefix(DeleteBuildingFragment __instance)
        {
            if (!__instance.SelectedBuildingIsDeletable()) return true;
            if (!__instance._selectedBlockObject) return true;

            return ReplayEvent.DoEntityPrefix(__instance._selectedBlockObject, entityID =>
            {
                return new BuildingsDeconstructedEvent()
                {
                    entityIDs = new List<string>() { entityID },
                };
            });
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

        public override string ToActionString()
        {
            return $"Building {entityID} paused set to: {wasPaused}";
        }
    }

    [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.Pause))]
    class PausableBuildingPausePatcher
    {
        static bool Prefix(PausableBuilding __instance)
        {
            // Don't record if already paused
            if (__instance.Paused) return true;
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new BuildingPausedChangedEvent()
                {
                    entityID = entityID,
                    wasPaused = true,
                };
            });
        }
    }

    [HarmonyPatch(typeof(PausableBuilding), nameof(PausableBuilding.Resume))]
    class PausableBuildingResumePatcher
    {
        static bool Prefix(PausableBuilding __instance)
        {
            // Don't record if already unpaused
            if (!__instance.Paused) return true;
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new BuildingPausedChangedEvent()
                {
                    entityID = entityID,
                    wasPaused = false,
                };
            });
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

        public override string ToActionString()
        {
            return $"Setting priority for {entityID} to: {priority}";
        }

        public static bool DoPrefix(BaseComponent __instance, Timberborn.PrioritySystem.Priority priority, Func<PriorityChangedEvent<T>> constructor)
        {
            return DoEntityPrefix(__instance, entityID =>
            {
                var evt = constructor();
                evt.entityID = entityID;
                evt.priority = priority;
                return evt;
            });
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

        public override string ToActionString()
        {
            return $"Changing desired workers for {entityID} - increasing: {increased}";
        }


        public static bool DoPrefix(Workplace __instance, bool increased)
        {
            // Ignore if we're already at max/min workers
            if (increased && __instance.DesiredWorkers >= __instance._workplaceSpecification.MaxWorkers) return true;
            if (!increased && __instance.DesiredWorkers <= 1) return true;

            return DoEntityPrefix(__instance, entityID =>
            {
                return new WorkplaceDesiredWorkersChangedEvent()
                {
                    entityID = entityID,
                    increased = increased,
                };
            });
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

    class FloodgateHeightChangedEvent : ReplayEvent
    {
        public string entityID;
        public float height;

        public override void Replay(IReplayContext context)
        {
            Floodgate floodgate = GetComponent<Floodgate>(context, entityID);
            if (!floodgate) return;
            floodgate.SetHeightAndSynchronize(height);
        }

        public override string ToActionString()
        {
            return $"Setting floodgate {entityID} height to: {height}";
        }
    }

    [HarmonyPatch(typeof(Floodgate), nameof(Floodgate.SetHeightAndSynchronize))]
    class FloodgateSetHeightPatcher
    {
        static bool Prefix(Floodgate __instance, float newHeight)
        {
            // Ignore if height is already new height
            if (__instance.Height == newHeight) return true;

            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new FloodgateHeightChangedEvent()
                {
                    entityID = entityID,
                    height = newHeight,
                };
            });
        }
    }

    class FloodgateSynchronizedChangedEvent : ReplayEvent
    {
        public string entityID;
        public bool isSynchronized;

        public override void Replay(IReplayContext context)
        {
            Floodgate floodgate = GetComponent<Floodgate>(context, entityID);
            if (!floodgate) return;
            floodgate.ToggleSynchronization(isSynchronized);
        }

        public override string ToActionString()
        {
            return $"Setting floodgate synchronized for {entityID} to: {isSynchronized}";
        }
    }


    [HarmonyPatch(typeof(Floodgate), nameof(Floodgate.ToggleSynchronization))]
    class FloodgateSynchronizationPatcher
    {
        static bool Prefix(Floodgate __instance, bool newValue)
        {
            // Ignore if height is already the same
            if (__instance.IsSynchronized == newValue) return true;

            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new FloodgateSynchronizedChangedEvent()
                {
                    entityID = entityID,
                    isSynchronized = newValue,
                };
            });

        }
    }

    enum StockpilePriority
    {
        Accept,
        Empty,
        Obtain,
        Supply
    }

    [Serializable]
    class StockpilePriorityChangedEvent : ReplayEvent
    {
        public string entityID;
        public StockpilePriority priority;

        public override void Replay(IReplayContext context)
        {
            var emptiable = GetComponent<Emptiable>(context, entityID);
            if (emptiable != null)
            {
                if (priority == StockpilePriority.Empty) emptiable.MarkForEmptying();
                else emptiable.UnmarkForEmptying();
            }

            var obtainer = GetComponent<GoodObtainer>(context, entityID);
            if (obtainer != null)
            {
                if (priority == StockpilePriority.Obtain) obtainer.EnableGoodObtaining();
                else obtainer.DisableGoodObtaining();
            }

            var supplier = GetComponent<GoodSupplier>(context, entityID);
            if (supplier != null)
            {
                if (priority == StockpilePriority.Supply) supplier.EnableSupplying();
                else supplier.DisableSupplying();
            }
        }

        public override string ToActionString()
        {
            return $"Setting good priority for {entityID} to: {priority}";
        }

        public static bool DoPrefix(StockpilePriorityToggle toggle, StockpilePriority priority)
        {
            return DoEntityPrefix(toggle._goodObtainer, entityID =>
            {
                return new StockpilePriorityChangedEvent()
                {
                    entityID = entityID,
                    priority = priority,
                };
            });
        }
    }

    [HarmonyPatch(typeof(StockpilePriorityToggle), nameof(StockpilePriorityToggle.AcceptClicked))]
    class StockpilePriorityToggleActive
    {
        static bool Prefix(StockpilePriorityToggle __instance)
        {
            return StockpilePriorityChangedEvent.DoPrefix(__instance, StockpilePriority.Accept);
        }
    }

    [HarmonyPatch(typeof(StockpilePriorityToggle), nameof(StockpilePriorityToggle.EmptyClicked))]
    class StockpilePriorityToggleEmpty
    {
        static bool Prefix(StockpilePriorityToggle __instance)
        {
            return StockpilePriorityChangedEvent.DoPrefix(__instance, StockpilePriority.Empty);
        }
    }

    [HarmonyPatch(typeof(StockpilePriorityToggle), nameof(StockpilePriorityToggle.ObtainClicked))]
    class StockpilePriorityToggleObtain
    {
        static bool Prefix(StockpilePriorityToggle __instance)
        {
            return StockpilePriorityChangedEvent.DoPrefix(__instance, StockpilePriority.Obtain);
        }
    }

    [HarmonyPatch(typeof(StockpilePriorityToggle), nameof(StockpilePriorityToggle.SupplyClicked))]
    class StockpilePriorityToggleSupply
    {
        static bool Prefix(StockpilePriorityToggle __instance)
        {
            return StockpilePriorityChangedEvent.DoPrefix(__instance, StockpilePriority.Supply);
        }
    }



    // For debugging only - to figure out where dropdowns are being set
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

    // For debugging only - to figure out where dropdowns are being set
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
