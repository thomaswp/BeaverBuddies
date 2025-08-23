﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BeaversUI;
using Timberborn.BuilderPrioritySystem;
using Timberborn.BuildingsBlocking;
using Timberborn.BuildingsUI;
using Timberborn.Characters;
using Timberborn.CharactersUI;
using Timberborn.CoreUI;
using Timberborn.Demolishing;
using Timberborn.DemolishingUI;
using Timberborn.Emptying;
using Timberborn.EntityPanelSystem;
using Timberborn.EntitySystem;
using Timberborn.Explosions;
using Timberborn.ExplosionsUI;
using Timberborn.Fields;
using Timberborn.Forestry;
using Timberborn.GameDistrictsUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameStartup;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.Hauling;
using Timberborn.HaulingUI;
using Timberborn.InventorySystem;
using Timberborn.Planting;
using Timberborn.PrefabSystem;
using Timberborn.PrioritySystem;
using Timberborn.RecoveredGoodSystem;
using Timberborn.RecoveredGoodSystemUI;
using Timberborn.SettlementNameSystemUI;
using Timberborn.SingletonSystem;
using Timberborn.StockpilePrioritySystem;
using Timberborn.StockpilePriorityUISystem;
using Timberborn.WaterBuildings;
using Timberborn.WaterBuildingsUI;
using Timberborn.WaterSourceSystem;
using Timberborn.WaterSourceSystemUI;
using Timberborn.Wonders;
using Timberborn.WondersUI;
using Timberborn.WorkerTypesUI;
using Timberborn.Workshops;
using Timberborn.WorkSystem;
using Timberborn.WorkSystemUI;
using Timberborn.ZiplineSystem;
using Timberborn.ZiplineSystemUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Events
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
            GatherableSpec gatherable = null;
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
        static bool Prefix(GatherablePrioritizer __instance, GatherableSpec gatherableSpec)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                var name = gatherableSpec?.GetComponentFast<PrefabSpec>()?.PrefabName;
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
            RecipeSpec recipe = null;
            if (itemID != null)
            {
                recipe = context.GetSingleton<RecipeSpecService>()?.GetRecipe(itemID);
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
        static bool Prefix(Manufactory __instance, RecipeSpec selectedRecipe)
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
            PlantableSpec plantable = null;
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
        static bool Prefix(PlantablePrioritizer __instance, PlantableSpec plantableSpec)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                var id = plantableSpec?.PrefabName;

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
            if (increased && __instance.DesiredWorkers >= __instance._workplaceSpec.MaxWorkers) return true;
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

    [Serializable]
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


    [Serializable]
    class DemolishButtonClickedEvent : ReplayEvent
    {
        public string entityID;
        public bool mark;

        public override void Replay(IReplayContext context)
        {
            Demolishable demolishable = GetComponent<Demolishable>(context, entityID);
            if (!demolishable) return;
            if (mark)
            {
                demolishable.Mark();
            }
            else
            {
                demolishable.Unmark();
            }
        }

        public override string ToActionString()
        {
            string verb = mark ? "Marking" : "Unmarking";
            return $"{verb} {entityID} for demolition";
        }
    }

    [HarmonyPatch(typeof(DemolishableFragment), nameof(DemolishableFragment.OnDemolishButtonClick))]
    class DemolishableFragmentButtonClickedPatcher
    {
        static bool Prefix(DemolishableFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._demolishable, entityID =>
            {
                return new DemolishButtonClickedEvent()
                {
                    entityID = entityID,
                    mark = !__instance._demolishable.IsMarked,
                };
            });
        }
    }

    [Serializable]
    class DynamiteTriggeredEvent : ReplayEvent
    {
        public string entityID;

        public override void Replay(IReplayContext context)
        {
            Dynamite dynamite = GetComponent<Dynamite>(context, entityID);
            if (!dynamite) return;
            dynamite.Trigger();
        }

        public override string ToActionString()
        {
            return $"Triggering dynamite {entityID}!!";
        }
    }

    [HarmonyPatch(typeof(DynamiteFragment), nameof(DynamiteFragment.DetonateSelectedDynamite), [])]
    class DynamiteFragmentDetonateSelectedDynamitePatcher
    {
        static bool Prefix(DynamiteFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._dynamite, entityID =>
            {
                return new DynamiteTriggeredEvent()
                {
                    entityID = entityID,
                };
            });
        }
    }

    [Serializable]
    class GoodStackDeletedEvent : ReplayEvent
    {
        public string entityID;

        public override void Replay(IReplayContext context)
        {
            RecoveredGoodStack goodStack = GetComponent<RecoveredGoodStack>(context, entityID);
            if (!goodStack) return;
            context.GetSingleton<EntityService>().Delete(goodStack);
        }

        public override string ToActionString()
        {
            return $"Deleting recoverable good {entityID}";
        }
    }

    [HarmonyPatch(typeof(DeleteRecoveredGoodStackFragment), nameof(DeleteRecoveredGoodStackFragment.DeleteRecoveredGoodStack))]
    class DeleteRecoveredGoodStackFragmentPatcher
    {
        static bool Prefix(DeleteRecoveredGoodStackFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._recoveredGoodStack, entityID =>
            {
                return new GoodStackDeletedEvent()
                {
                    entityID = entityID,
                };
            });
        }
    }

    [Serializable]
    class EntityRenamedEvent : ReplayEvent
    {
        public string entityID;
        public string newName;

        public override void Replay(IReplayContext context)
        {
            GetComponent<IModifiableEntityBadge>(context, entityID)?.SetEntityName(newName);
        }

        public override string ToActionString()
        {
            return $"Renaming {entityID} to {newName}";
        }
    }

    [HarmonyPatch(typeof(EntityPanel), nameof(EntityPanel.SetEntityName))]
    [ManualMethodOverwrite]
    /**
     *  6/20/2025
		if ((bool)_shownEntity && !string.IsNullOrWhiteSpace(newName))
		{
			_entityBadgeService.SetEntityName(_shownEntity, newName.Trim());
		}
     */
    class EntityPanelSetEntityNamePatcher
    {
        static bool Prefix(EntityPanel __instance, string newName)
        {
            // If the name / change is invalid, we don't record it and use the default behavior.
            // We capture the UI hook, rather than EntityBadgeService, in case the latter is use
            // in game logic.
            if (!((bool)__instance._shownEntity && !string.IsNullOrWhiteSpace(newName)))
            {
                return true;
            }
            return ReplayEvent.DoEntityPrefix(__instance._shownEntity, entityID =>
            {
                return new EntityRenamedEvent()
                {
                    entityID = entityID,
                    newName = newName,
                };
            });
        }
    }

    [HarmonyPatch(typeof(CharacterBatchControlRowItemFactory), nameof(CharacterBatchControlRowItemFactory.SetEntityName))]
    [ManualMethodOverwrite]
    /**
     * Mirror of EntityPanel.SetEntityName capture, routed through batch control row factory.
     *  6/20/2025
		if ((bool)_shownEntity && !string.IsNullOrWhiteSpace(newName))
		{
			_entityBadgeService.SetEntityName(_shownEntity, newName.Trim());
		}
     */
    class CharacterBatchControlRowItemFactorySetEntityNamePatcher
    {
        static bool Prefix(CharacterBatchControlRowItemFactory __instance, string newName, Character character)
        {
            if (!(character != null && !string.IsNullOrWhiteSpace(newName))) return true;
            return ReplayEvent.DoEntityPrefix(character, entityID => new EntityRenamedEvent()
            {
                entityID = entityID,
                newName = newName,
            });
        }
    }

    class WorkerTypeUnlockedEvent : ReplayEvent
    {
        public UnlockableWorkerType workerType;

        public override void Replay(IReplayContext context)
        {
            var service = context.GetSingleton<WorkplaceUnlockingService>();
            if (service.Unlocked(workerType))
            {
                Plugin.LogWarning($"Tried to unlock {workerType.WorkerType} for {workerType.WorkplacePrefabName} but it was already unlocked");
                return;
            }
            service.Unlock(workerType);
        }

        public override string ToActionString()
        {
            return $"Unlocking {workerType.WorkerType} for {workerType.WorkplacePrefabName}";
        }
    }

    [HarmonyPatch(typeof(WorkplaceUnlockingService), nameof(WorkplaceUnlockingService.Unlock))]
    class WorkplaceUnlockingServiceUnlockPatcher
    {
        static bool Prefix(WorkplaceUnlockingService __instance, UnlockableWorkerType unlockableWorkerType)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new WorkerTypeUnlockedEvent()
                {
                    workerType = unlockableWorkerType,
                };
            });
        }
    }

    class WorkerTypeSetEvent : ReplayEvent
    {
        public string workplaceEntityID;
        public string workerType;

        public override void Replay(IReplayContext context)
        {
            var workplace = GetComponent<WorkplaceWorkerType>(context, workplaceEntityID);
            if (workplace == null) return;
            workplace.SetWorkerType(workerType);
        }

        public override string ToActionString()
        {
            return $"Setting worker type of {workplaceEntityID} to {workerType}";
        }
    }

    [HarmonyPatch(typeof(WorkplaceWorkerType), nameof(WorkplaceWorkerType.SetWorkerType))]
    class WorkplaceWorkerTypeSetWorkerTypePatcher
    {
        static bool Prefix(WorkplaceWorkerType __instance, string workerType)
        {
            return ReplayEvent.DoEntityPrefix(__instance, (entityID) =>
            {
                return new WorkerTypeSetEvent()
                {
                    workplaceEntityID = entityID,
                    workerType = workerType,
                };
            });
        }
    }

    [ManualMethodOverwrite]
    /*
        04/19/2025
		UnlockableWorkerType botUnlockableWorkerType = GetBotUnlockableWorkerType();
		_workplaceUnlockingDialogService.TryToUnlockWorkerType(botUnlockableWorkerType, SetBotWorkerType);
     */
    [HarmonyPatch(typeof(WorkerTypeToggle), nameof(WorkerTypeToggle.TryToUnlock))]
    class WorkerTypeToggleTryToUnlockPatcher
    {
        static bool Prefix(WorkerTypeToggle __instance)
        {
            // Do the method as normal, but instead of calling back SetBotWorkerType, we raise
            // an event to do so (only if we get dialot confirmation).
            UnlockableWorkerType botUnlockableWorkerType = __instance.GetBotUnlockableWorkerType();
            __instance._workplaceUnlockingDialogService.TryToUnlockWorkerType(botUnlockableWorkerType, () =>
            {
                bool callOriginal = ReplayEvent.DoEntityPrefix(__instance._workplaceWorkerType, (entityID) =>
                {
                    return new WorkerTypeSetEvent()
                    {
                        workplaceEntityID = entityID,
                        workerType = WorkerTypeHelper.BotWorkerType,
                    };
                });

                // If the original method should be called, we call it here.
                if (callOriginal)
                {
                    __instance.SetBotWorkerType();
                }
            });

            // Always override
            return false;
        }
    }

    [Serializable]
    class HaulPrioritizablePrioritizedEvent : ReplayEvent
    {
        public string entityID;
        public bool prioritized;

        public override void Replay(IReplayContext context)
        {
            var prioritizer = GetComponent<HaulPrioritizable>(context, entityID);
            if (!prioritizer) return;
            prioritizer.Prioritized = prioritized;
            // Probably can't update the UI an any reasonable way, but I'm guessing
            // the other players won't have the relevant UI open at the same time.
        }

        public override string ToActionString()
        {
            return $"Setting haul priority for {entityID} to: {prioritized}";
        }
    }


    [HarmonyPatch(typeof(HaulPrioritizable), nameof(HaulPrioritizable.Prioritized), MethodType.Setter)]
    class HaulPrioritizablePrioritizedPatcher
    {
        public static bool Prefix(HaulPrioritizable __instance, bool value)
        {
            if (value == __instance.Prioritized) return true;
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new HaulPrioritizablePrioritizedEvent()
                {
                    entityID = entityID,
                    prioritized = value,
                };
            });
        }
    }

    [Serializable]
    class ToggleForresterReplantDeadTreesEvent : ReplayEvent
    {
        public string entityID;
        public bool shouldReplant;

        public override void Replay(IReplayContext context)
        {
            var forester = GetComponent<Forester>(context, entityID);
            if (!forester) return;
            forester.ReplantDeadTrees = shouldReplant;
        }

        public override string ToActionString()
        {
            return $"Setting replant dead trees for Forrester {entityID} to: {shouldReplant}";
        }
    }

    [HarmonyPatch(typeof(Forester), nameof(Forester.SetReplantDeadTrees))]
    class ForesterSetReplantDeadTreesPatcher
    {
        public static bool Prefix(Forester __instance, bool replantDeadTrees)
        {
            if (__instance.ReplantDeadTrees == replantDeadTrees) return true;
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new ToggleForresterReplantDeadTreesEvent()
                {
                    entityID = entityID,
                    shouldReplant = replantDeadTrees,
                };
            });
        }
    }

    public enum SluiceToggleType
    {
        WaterLevel,
        AboveContamination,
        BelowContamination,
        Synchronization,
    }

    [Serializable]
    class SluicePlainToggleUpdatedEvent : ReplayEvent
    {
        public string entityID;
        public bool value;
        public SluiceToggleType toggleType;

        public override void Replay(IReplayContext context)
        {
            var sluice = GetComponent<Sluice>(context, entityID);
            if (!sluice) return;
            var sluiceState = sluice._sluiceState;
            if (toggleType == SluiceToggleType.WaterLevel)
            {
                if (value)
                {
                    sluiceState.EnableAutoCloseOnOutflow();
                }
                else
                {
                    sluiceState.DisableAutoCloseOnOutflow();
                }
            }
            else if (toggleType == SluiceToggleType.AboveContamination)
            {
                if (value)
                {
                    sluiceState.EnableAutoCloseOnAbove();
                }
                else
                {
                    sluiceState.DisableAutoCloseOnAbove();
                }
            }
            else if (toggleType == SluiceToggleType.BelowContamination)
            {
                if (value)
                {
                    sluiceState.EnableAutoCloseOnBelow();
                }
                else
                {
                    sluiceState.DisableAutoCloseOnBelow();
                }
            }
            else if (toggleType == SluiceToggleType.Synchronization)
            {
                sluiceState.ToggleSynchronization(value);
                // This won't update the Fragment's UI like it's supposed to
                // (I don't think we can get a reference here easily),
                // but it should update if the user tries to slide the slider,
                // and I don't think it affects game logic.
            }
        }

        public override string ToActionString()
        {
            return $"Setting sluice {entityID} {toggleType} toggle to: {value}";
        }

        public static bool DoPrefix(Sluice __instance, bool value, SluiceToggleType toggleType)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new SluicePlainToggleUpdatedEvent()
                {
                    entityID = entityID,
                    value = value,
                    toggleType = toggleType,
                };
            });
        }
    }

    [HarmonyPatch(typeof(SluiceFragment), nameof(SluiceFragment.OnWaterLevelToggleChanged))]
    class SluiceFragmentOnWaterLevelToggleChangedPatcher
    {
        public static bool Prefix(SluiceFragment __instance, ChangeEvent<bool> evt)
        {
            return SluicePlainToggleUpdatedEvent.DoPrefix(
                __instance._sluice, evt.newValue, SluiceToggleType.WaterLevel);
        }
    }

    [HarmonyPatch(typeof(SluiceFragment), nameof(SluiceFragment.OnAboveContaminationToggleChanged))]
    class SluiceFragmentOnAboveContaminationToggleChangedPatcher
    {
        public static bool Prefix(SluiceFragment __instance, ChangeEvent<bool> evt)
        {
            return SluicePlainToggleUpdatedEvent.DoPrefix(
                __instance._sluice, evt.newValue, SluiceToggleType.AboveContamination);
        }
    }

    [HarmonyPatch(typeof(SluiceFragment), nameof(SluiceFragment.OnBelowContaminationToggleChanged))]
    class SluiceFragmentOnBelowContaminationToggleChangedPatcher
    {
        public static bool Prefix(SluiceFragment __instance, ChangeEvent<bool> evt)
        {
            return SluicePlainToggleUpdatedEvent.DoPrefix(
                __instance._sluice, evt.newValue, SluiceToggleType.BelowContamination);
        }
    }

    [HarmonyPatch(typeof(SluiceFragment), nameof(SluiceFragment.ToggleSynchronization))]
    class SluiceFragmentToggleSynchronizationChangedPatcher
    {
        public static bool Prefix(SluiceFragment __instance, ChangeEvent<bool> evt)
        {
            return SluicePlainToggleUpdatedEvent.DoPrefix(
                __instance._sluice, evt.newValue, SluiceToggleType.Synchronization);
        }
    }

    public enum SluiceLimitSliderType
    {
        Outflow,
        AboveContamination,
        BelowContamination,
    }

    [Serializable]
    class SluiceSliderUpdatedEvent : ReplayEvent
    {
        public string entityID;
        public SluiceLimitSliderType sliderType;
        public float value;

        public override void Replay(IReplayContext context)
        {
            var sluice = GetComponent<Sluice>(context, entityID);
            if (!sluice) return;
            var sluiceState = sluice._sluiceState;
            switch (sliderType)
            {
                case SluiceLimitSliderType.Outflow:
                    sluiceState.SetOutflowLimit(value);
                    break;
                case SluiceLimitSliderType.AboveContamination:
                    sluiceState.SetAboveContaminationLimit(value);
                    break;
                case SluiceLimitSliderType.BelowContamination:
                    sluiceState.SetBelowContaminationLimit(value);
                    break;
            }
        }

        public override string ToActionString()
        {
            string type = sliderType.ToString();
            return $"Setting sluice {entityID} {type} limit to: {value}";
        }

        public static bool DoPrefix(SluiceState __instance, SluiceLimitSliderType sliderType, float value)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new SluiceSliderUpdatedEvent()
                {
                    entityID = entityID,
                    sliderType = sliderType,
                    value = value,
                };
            });
        }
    }

    /*
     * Note: We override the SluiceState methods instead of the SluiceFragment methods
     * only for contaminatoin limits, because these methods seem to only be called form the
     * UI, which contains some additional logic we don't want to have to manually override.
     * This is not the case for the Outflow, which is called on SluiceState.Tick(), so we
     * have to override SluiceFragment.ChangeFlow() instead, and duplicate that logic.
     */

    [HarmonyPatch(typeof(SluiceState), nameof(SluiceState.SetBelowContaminationLimit))]
    class SluiceStateSetBelowContaminationLimitPatcher
    {
        public static bool Prefix(SluiceState __instance, float contaminationLimit)
        {
            return SluiceSliderUpdatedEvent.DoPrefix(
                __instance, SluiceLimitSliderType.BelowContamination, contaminationLimit
            );
        }
    }

    [HarmonyPatch(typeof(SluiceState), nameof(SluiceState.SetAboveContaminationLimit))]
    class SluiceStateSetAboveContaminationLimitPatcher
    {
        public static bool Prefix(SluiceState __instance, float contaminationLimit)
        {
            return SluiceSliderUpdatedEvent.DoPrefix(
                __instance, SluiceLimitSliderType.AboveContamination, contaminationLimit
            );
        }
    }

    [ManualMethodOverwrite]
    /*
     * 04/25/2025
	float num = UpdateWaterLevelSliderValue(newHeight);
	if (WaterLevelSliderValue != num)
	{
		_sluiceState.SetOutflowLimit(num - (float)Range);
	}
    */
    [HarmonyPatch(typeof(SluiceFragment), nameof(SluiceFragment.ChangeFlow))]
    class SluiceFragmentChangeFlowPatcher
    {
        public static bool Prefix(SluiceFragment __instance, float newHeight)
        {
            // Note that UpdateWaterLevelSliderValue does modify the slider value, so this
            // is UI logic we need to keep updated if the method changes.
            float num = __instance.UpdateWaterLevelSliderValue(newHeight);
            if (__instance.WaterLevelSliderValue == num) return true;
            return SluiceSliderUpdatedEvent.DoPrefix(
                // Note: it's num - Range, not num
                __instance._sluice._sluiceState, SluiceLimitSliderType.Outflow, num - (float)__instance.Range
            );
        }
    }

    public enum SluiceMode
    {
        Auto,
        Open,
        Closed
    }

    [Serializable]
    class SluiceModeUpdatedEvent : ReplayEvent
    {
        public string entityID;
        public SluiceMode mode;

        public override void Replay(IReplayContext context)
        {
            var sluice = GetComponent<Sluice>(context, entityID);
            if (!sluice) return;
            var sluiceState = sluice._sluiceState;
            switch (mode)
            {
                case SluiceMode.Auto:
                    sluiceState.SetAuto();
                    break;
                case SluiceMode.Open:
                    sluiceState.Open();
                    break;
                case SluiceMode.Closed:
                    sluiceState.Close();
                    break;
            }
        }

        public override string ToActionString()
        {
            return $"Setting sluice {entityID} mode to: {mode}";
        }

        public static bool DoPrefix(SluiceState __instance, SluiceMode mode)
        {
            return ReplayEvent.DoEntityPrefix(__instance, entityID =>
            {
                return new SluiceModeUpdatedEvent()
                {
                    entityID = entityID,
                    mode = mode,
                };
            });
        }
    }

    [HarmonyPatch(typeof(SluiceState), nameof(SluiceState.SetAuto))]
    class SluiceStateSetAutoPatcher
    {
        public static bool Prefix(SluiceState __instance)
        {
            return SluiceModeUpdatedEvent.DoPrefix(__instance, SluiceMode.Auto);
        }
    }

    [HarmonyPatch(typeof(SluiceState), nameof(SluiceState.Open))]
    class SluiceStateOpenPatcher
    {
        public static bool Prefix(SluiceState __instance)
        {
            return SluiceModeUpdatedEvent.DoPrefix(__instance, SluiceMode.Open);
        }
    }

    [HarmonyPatch(typeof(SluiceState), nameof(SluiceState.Close))]
    class SluiceStateClosePatcher
    {
        public static bool Prefix(SluiceState __instance)
        {
            return SluiceModeUpdatedEvent.DoPrefix(__instance, SluiceMode.Closed);
        }
    }

    enum WaterInputDepthAction
    {
        ToggleLimit,
        IncreaseDepthLimit,
        DecreaseDepthLimit,
    }

    [Serializable]
    class WaterInputDepthActionEvent : ReplayEvent
    {
        public string entityID;
        public WaterInputDepthAction action;

        public override void Replay(IReplayContext context)
        {
            var waterinput = GetComponent<WaterInputCoordinates>(context, entityID);
            var waterInputSpec = GetComponent<WaterInputSpec>(context, entityID);
            if (waterinput == null || waterInputSpec == null) return;
            switch (action)
            {
                case WaterInputDepthAction.ToggleLimit:
                    if (waterinput.UseDepthLimit)
                    {
                        waterinput.DisableDepthLimit();
                    }
                    else
                    {
                        waterinput.SetDepthLimit(waterinput.Depth);
                    }
                    break;
                case WaterInputDepthAction.IncreaseDepthLimit:
                    int depthLimit = Math.Min(waterInputSpec.MaxDepth, waterinput.DepthLimit + 1);
                    waterinput.SetDepthLimit(depthLimit);
                    break;
                case WaterInputDepthAction.DecreaseDepthLimit:
                    depthLimit = Math.Max(0, waterinput.DepthLimit - 1);
                    waterinput.SetDepthLimit(depthLimit);
                    break;
            }
        }

        public static bool DoPrefix(BaseComponent entityComponent, WaterInputDepthAction action)
        {
            return DoEntityPrefix(entityComponent, entityID =>
            {
                return new WaterInputDepthActionEvent()
                {
                    entityID = entityID,
                    action = action,
                };
            });
        }

        public override string ToActionString()
        {
            return $"Water input {entityID} action={action}";
        }
    }

    [HarmonyPatch(typeof(WaterInputDepthFragment), nameof(WaterInputDepthFragment.ToggleDepthLimit))]
    class WaterInputDepthFragmentToggleDepthLimitPatcher
    {
        public static bool Prefix(WaterInputDepthFragment __instance)
        {
            return WaterInputDepthActionEvent.DoPrefix(__instance._waterInputCoordinates, WaterInputDepthAction.ToggleLimit);
        }
    }

    [HarmonyPatch(typeof(WaterInputDepthFragment), nameof(WaterInputDepthFragment.IncreaseDepth))]
    class WaterInputDepthFragmentIncreaseDepthPatcher
    {
        public static bool Prefix(WaterInputDepthFragment __instance)
        {
            return WaterInputDepthActionEvent.DoPrefix(__instance._waterInputCoordinates, WaterInputDepthAction.IncreaseDepthLimit);
        }
    }

    [HarmonyPatch(typeof(WaterInputDepthFragment), nameof(WaterInputDepthFragment.DecreaseDepth))]
    class WaterInputDepthFragmentDecreaseDepthPatcher
    {
        public static bool Prefix(WaterInputDepthFragment __instance)
        {
            return WaterInputDepthActionEvent.DoPrefix(__instance._waterInputCoordinates, WaterInputDepthAction.DecreaseDepthLimit);
        }
    }

    [Serializable]
    class ZiplineConnectionChangedEvent : ReplayEvent
    {
        public string currentTowerEntityID;
        public string otherTowerEntityID;
        public bool add;

        public override void Replay(IReplayContext context)
        {
            var currentTower = GetComponent<ZiplineTower>(context, currentTowerEntityID);
            var otherTower = GetComponent<ZiplineTower>(context, otherTowerEntityID);
            if (!currentTower || !otherTower) return;
            ZiplineConnectionService ziplineConnectionService = context.GetSingleton<ZiplineConnectionService>();
            if (add)
            {
                if (!ziplineConnectionService.CanBeConnected(currentTower, otherTower))
                {
                    Plugin.LogError($"Tried to connect {currentTowerEntityID} to {otherTowerEntityID}, but it was not possible");
                    return;
                }
                ziplineConnectionService.Connect(currentTower, otherTower);
            }
            else
            {
                ziplineConnectionService.Disconnect(currentTower, otherTower);
            }
        }

        public override string ToActionString()
        {
            string verb = add ? "Connecting" : "Disconnecting";
            return $"{verb} zipline connection from {currentTowerEntityID} to {otherTowerEntityID}";
        }
    }

    [HarmonyPatch(typeof(ZiplineConnectionAddingTool), nameof(ZiplineConnectionAddingTool.Connect))]
    class ZiplineConnectionAddingToolConnectPatcher
    {
        public static bool Prefix(ZiplineConnectionAddingTool __instance, ZiplineTower ziplineTower)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new ZiplineConnectionChangedEvent()
                {
                    currentTowerEntityID = ReplayEvent.GetEntityID(__instance._currentZiplineTower),
                    otherTowerEntityID = ReplayEvent.GetEntityID(ziplineTower),
                    add = true,
                };
            });
        }
    }

    [HarmonyPatch(typeof(ZiplineConnectionButtonFactory), nameof(ZiplineConnectionButtonFactory.RemoveConnection))]
    class ZiplineConnectionButtonFactoryRemoveConnectionPatcher
    {
        public static bool Prefix(ZiplineTower owner, ZiplineTower otherZiplineTower)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new ZiplineConnectionChangedEvent()
                {
                    currentTowerEntityID = ReplayEvent.GetEntityID(owner),
                    otherTowerEntityID = ReplayEvent.GetEntityID(otherZiplineTower),
                    add = false,
                };
            });
        }
    }

    [Serializable]
    class WonderActivatedEvent : ReplayEvent
    {
        public string entityID;

        public override void Replay(IReplayContext context)
        {
            GetComponent<Wonder>(context, entityID)?.Activate();
        }

        public override string ToActionString()
        {
            return $"Activating wonder {entityID}";
        }
    }

    [HarmonyPatch(typeof(WonderFragment), nameof(WonderFragment.ActivateWonder))]
    class WonderFragmentActivateWonderPatcher
    {
        public static bool Prefix(WonderFragment __instance, ClickEvent evt)
        {
            return ReplayEvent.DoEntityPrefix(__instance._wonder, entityID =>
            {
                return new WonderActivatedEvent()
                {
                    entityID = entityID,
                };
            });
        }
    }

    [Serializable]
    class DefaultWorkerTypeChangedEvent : ReplayEvent
    {
        public string entityID;
        public string workerType;

        public override void Replay(IReplayContext context)
        {
            GetComponent<DistrictDefaultWorkerType>(context, entityID)?.SetWorkerType(workerType);
        }

        public override string ToActionString()
        {
            return $"Setting default worker type for {entityID} to {workerType}";
        }

        public static bool DoPrefix(DistrictCenterFragment __instance, string workerType)
        {
            return DoEntityPrefix(__instance._districtCenter, (entityID) =>
            {
                return new DefaultWorkerTypeChangedEvent()
                {
                    entityID = entityID,
                    workerType = workerType,
                };
            });
        }
    }

    [HarmonyPatch(typeof(DistrictCenterFragment), nameof(DistrictCenterFragment.SetBeaverWorkerType))]
    class DistrictCenterFragmentSetBeaverWorkerTypePatcher
    {
        public static bool Prefix(DistrictCenterFragment __instance)
        {
            return DefaultWorkerTypeChangedEvent.DoPrefix(__instance, WorkerTypeHelper.BeaverWorkerType);
        }
    }

    [HarmonyPatch(typeof(DistrictCenterFragment), nameof(DistrictCenterFragment.SetBotWorkerType))]
    class DistrictCenterFragmentSetBotWorkerTypePatcher
    {
        public static bool Prefix(DistrictCenterFragment __instance)
        {
            return DefaultWorkerTypeChangedEvent.DoPrefix(__instance, WorkerTypeHelper.BotWorkerType);
        }
    }

    [Serializable]
    class WaterSourceRegulatorStateChangedEvent : ReplayEvent
    {
        public string entityID;
        public bool isOpen;

        public override void Replay(IReplayContext context)
        {
            var regulator = GetComponent<WaterSourceRegulator>(context, entityID);
            if (regulator == null) return;
            if (isOpen)
            {
                regulator.Open();
            }
            else
            {
                regulator.Close();
            }
        }

        public override string ToActionString()
        {
            string verb = isOpen ? "Opening" : "Closing";
            return $"{verb} water regulator {entityID}";
        }

        public static bool DoPrefix(WaterSourceRegulator __instance, bool isOpen)
        {
            return DoEntityPrefix(__instance, (entityID) =>
            {
                return new WaterSourceRegulatorStateChangedEvent()
                {
                    entityID = entityID,
                    isOpen = isOpen,
                };
            });
        }
    }

    // I can't find anywhere these methods are called outside of the UI, so it
    // seems safe to patch them directly.
    // The UI event itself is a bit ugly to patch.
    [HarmonyPatch(typeof(WaterSourceRegulator), nameof(WaterSourceRegulator.Open))]
    class WaterSourceRegulatorOpenPatcher
    {
        public static bool Prefix(WaterSourceRegulator __instance)
        {
            return WaterSourceRegulatorStateChangedEvent.DoPrefix(__instance, true);
        }
    }

    [HarmonyPatch(typeof(WaterSourceRegulator), nameof(WaterSourceRegulator.Close))]
    class WaterSourceRegulatorClosePatcher
    {
        public static bool Prefix(WaterSourceRegulator __instance)
        {
            return WaterSourceRegulatorStateChangedEvent.DoPrefix(__instance, false);
        }
    }
}
