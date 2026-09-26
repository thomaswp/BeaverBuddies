using BeaverBuddies.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BeaversUI;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Bots;
using Timberborn.BotsUI;
using Timberborn.Common;
using Timberborn.ConstructionSites;
using Timberborn.ConstructionSitesUI;
using Timberborn.Coordinates;
using Timberborn.DeteriorationSystem;
using Timberborn.DeteriorationSystemUI;
using Timberborn.DwellingSystem;
using Timberborn.DwellingSystemUI;
using Timberborn.EntitySystem;
using Timberborn.Explosions;
using Timberborn.ExplosionsUI;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.InventorySystemUI;
using Timberborn.Reproduction;
using Timberborn.Stockpiles;
using Timberborn.StockpilesUI;
using Timberborn.TemplateSystem;
using Timberborn.ToolSystem;
using Timberborn.WondersUI;
using UnityEngine;

namespace BeaverBuddies.DevTools
{
    // ---- Dev tools in the toolbar ----

    // Blocks dev tools that aren't synced before they can be used
    [HarmonyPatch(typeof(ToolService), nameof(ToolService.SwitchToolInternal))]
    class ToolServiceSwitchToolPatcher
    {
        static bool Prefix(ITool tool)
        {
            if (!(tool is IDevModeTool devTool) || !devTool.IsDevMode) return true;
            if (!DevToolsPolicy.IsRestricted) return true;

            // Synced by the events below (and the building deletion events)
            if (tool is BeaverGeneratorTool
                || tool is BotGeneratorTool
                || tool is EntityBlockObjectDeletionTool
                || tool is BlockObjectTool)
            {
                return DevToolsPolicy.CheckAllowed();
            }

            Plugin.Log($"Blocking dev tool in co-op: {tool.GetType().Name}");
            return DevToolsPolicy.Block();
        }
    }

    [Serializable]
    public class DevCharactersSpawnedEvent : DevToolEvent
    {
        public Vector3Int tileCoordinates;
        public int count;
        public bool isChild;
        public bool isBot;

        public override void Replay(IReplayContext context)
        {
            Vector3 position = CoordinateSystem.GridToWorldCentered(tileCoordinates);
            if (isBot)
            {
                var botFactory = context.GetSingleton<BotFactory>();
                for (int i = 0; i < count; i++)
                {
                    botFactory.Create(position);
                }
                return;
            }

            // Same as BeaverGeneratorTool.PlaceBeavers
            var beaverFactory = context.GetSingleton<BeaverFactory>();
            var random = context.GetSingleton<DevToolsService>().RandomNumberGenerator;
            for (int i = 0; i < count; i++)
            {
                float age = random.Range(0f, 1f);
                if (isChild)
                {
                    beaverFactory.CreateChild(position, age);
                }
                else
                {
                    beaverFactory.CreateAdult(position, age);
                }
            }
        }

        public override string ToActionString()
        {
            string what = isBot ? "bots" : isChild ? "child beavers" : "beavers";
            return $"Dev: spawning {count} {what} at {tileCoordinates}";
        }
    }

    [HarmonyPatch(typeof(BeaverGeneratorTool), nameof(BeaverGeneratorTool.PlaceBeavers))]
    class BeaverGeneratorToolPatcher
    {
        static bool Prefix(BeaverGeneratorTool __instance, bool isChild, int count)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                var coordinates = __instance._cursorCoordinatesPicker.Pick();
                if (!coordinates.HasValue) return null;
                return new DevCharactersSpawnedEvent()
                {
                    tileCoordinates = coordinates.Value.TileCoordinates,
                    count = count,
                    isChild = isChild,
                };
            });
        }
    }

    [HarmonyPatch(typeof(BotGeneratorTool), nameof(BotGeneratorTool.PlaceBots))]
    class BotGeneratorToolPatcher
    {
        static bool Prefix(BotGeneratorTool __instance, int count)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                var coordinates = __instance._cursorCoordinatesPicker.Pick();
                if (!coordinates.HasValue) return null;
                return new DevCharactersSpawnedEvent()
                {
                    tileCoordinates = coordinates.Value.TileCoordinates,
                    count = count,
                    isBot = true,
                };
            });
        }
    }

    /**
     * Dev-only templates that aren't buildings (ruins, relics, water
     * sources...) are placed finished by the default placer. Buildings
     * are already synced by BuildingPlacedEvent.
     */
    [Serializable]
    public class DevObjectPlacedEvent : DevToolEvent
    {
        public string templateName;
        public Vector3Int coordinates;
        public Orientation orientation;
        public bool isFlipped;

        public override void Replay(IReplayContext context)
        {
            var devTools = context.GetSingleton<DevToolsService>();
            var spec = devTools.TemplateService
                .GetAll<PlaceableBlockObjectSpec>()
                .FirstOrDefault(s => s.GetSpec<TemplateSpec>()?.TemplateName == templateName);
            if (spec == null)
            {
                Plugin.LogWarning($"Could not find template: {templateName}");
                return;
            }
            Placement placement = new Placement(coordinates, orientation,
                isFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
            devTools.DefaultBlockObjectPlacer.Place(new EntitySetup.Builder(spec.Blueprint), placement);
        }

        public override string ToActionString()
        {
            return $"Dev: placing {templateName} at {coordinates}";
        }
    }

    [HarmonyPatch(typeof(DefaultBlockObjectPlacer), nameof(DefaultBlockObjectPlacer.Place))]
    class DefaultBlockObjectPlacerPatcher
    {
        static bool Prefix(EntitySetup.Builder entitySetupBuilder, Placement placement)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                string templateName = entitySetupBuilder.Template.GetSpec<TemplateSpec>()?.TemplateName;
                if (templateName == null) return null;
                return new DevObjectPlacedEvent()
                {
                    templateName = templateName,
                    coordinates = placement.Coordinates,
                    orientation = placement.Orientation,
                    isFlipped = placement.FlipMode.IsFlipped,
                };
            });
        }
    }

    // ---- Dev buttons in the entity panel ----

    [Serializable]
    public class DevInventoryChangedEvent : DevToolEvent
    {
        public string entityID;
        public string inventoryName;
        public string goodId;
        public int amount;
        public bool take;

        public override void Replay(IReplayContext context)
        {
            var entity = GetEntityComponent(context, entityID);
            if (entity == null) return;
            var inventories = new List<Inventory>();
            entity.GetComponents(inventories);
            Inventory inventory = inventories.FirstOrDefault(i => i.ComponentName == inventoryName);
            var box = context.GetSingleton<ModifyInventoryBox>();
            if (inventory == null || box == null) return;

            // Reuse the box's own logic (e.g. district centers ignore
            // capacity) by pointing it at the recorded inventory
            Inventory previous = box._inventory;
            box._inventory = inventory;
            try
            {
                GoodAmount goodAmount = new GoodAmount(goodId, amount);
                if (take)
                {
                    box.TakeGood(goodAmount);
                }
                else
                {
                    box.GiveGood(goodAmount);
                }
            }
            finally
            {
                box._inventory = previous;
            }
        }

        public override string ToActionString()
        {
            return $"Dev: {(take ? "taking" : "giving")} {amount} {goodId} in {entityID}";
        }

        public static bool DoPrefix(ModifyInventoryBox box, GoodAmount goodAmount, bool take)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                string entityID = ReplayEvent.GetEntityID(box._inventory);
                if (entityID == null) return null;
                return new DevInventoryChangedEvent()
                {
                    entityID = entityID,
                    inventoryName = box._inventory.ComponentName,
                    goodId = goodAmount.GoodId,
                    amount = goodAmount.Amount,
                    take = take,
                };
            });
        }
    }

    [HarmonyPatch(typeof(ModifyInventoryBox), nameof(ModifyInventoryBox.GiveGood))]
    class ModifyInventoryBoxGivePatcher
    {
        static bool Prefix(ModifyInventoryBox __instance, GoodAmount goodAmount)
        {
            return DevInventoryChangedEvent.DoPrefix(__instance, goodAmount, false);
        }
    }

    [HarmonyPatch(typeof(ModifyInventoryBox), nameof(ModifyInventoryBox.TakeGood))]
    class ModifyInventoryBoxTakePatcher
    {
        static bool Prefix(ModifyInventoryBox __instance, GoodAmount goodAmount)
        {
            return DevInventoryChangedEvent.DoPrefix(__instance, goodAmount, true);
        }
    }

    [Serializable]
    public class DevEntityActionEvent : DevToolEvent
    {
        public const string FinishConstruction = "FinishConstruction";
        public const string ProgressWonder = "ProgressWonder";
        public const string SpawnNewborn = "SpawnNewborn";
        public const string ZeroDurability = "ZeroDurability";
        public const string DetonateCore = "DetonateCore";
        public const string DeleteCore = "DeleteCore";
        public const string FillStockpile = "FillStockpile";

        public string entityID;
        public string action;
        public float delay;

        public override void Replay(IReplayContext context)
        {
            var entity = GetEntityComponent(context, entityID);
            if (entity == null) return;

            switch (action)
            {
                case FinishConstruction:
                    {
                        var site = entity.GetComponent<ConstructionSite>();
                        if ((bool)(BaseComponent)(object)site && ((BaseComponent)(object)site).Enabled)
                        {
                            site.FinishNow();
                        }
                        break;
                    }
                case ProgressWonder:
                    entity.GetComponent<ConstructionSite>()?.IncreaseBuildTime(WonderDebugFragment.BuildTimeAmount);
                    break;
                case SpawnNewborn:
                    {
                        var dwelling = entity.GetComponent<Dwelling>();
                        if ((bool)dwelling && dwelling.Enabled && dwelling.HasFreeSlots)
                        {
                            context.GetSingleton<DevToolsService>().NewbornSpawner.SpawnChild(dwelling);
                        }
                        break;
                    }
                case ZeroDurability:
                    {
                        var deteriorable = entity.GetComponent<Deteriorable>();
                        if ((bool)(BaseComponent)(object)deteriorable && ((BaseComponent)(object)deteriorable).Enabled)
                        {
                            deteriorable.SetDeteriorationToZero();
                        }
                        break;
                    }
                case DetonateCore:
                    {
                        var core = entity.GetComponent<UnstableCore>();
                        if (!(bool)core) break;
                        core.GetComponent<UnstableCoreExplosionBlocker>().Disable();
                        if (delay > 0)
                        {
                            core.ActivateDelayed(delay);
                        }
                        else
                        {
                            core.Activate();
                        }
                        break;
                    }
                case DeleteCore:
                    {
                        var core = entity.GetComponent<UnstableCore>();
                        if (!(bool)core) break;
                        core.GetComponent<UnstableCoreExplosionBlocker>().BlockExplosion();
                        context.GetSingleton<EntityService>().Delete(core);
                        break;
                    }
                case FillStockpile:
                    {
                        // Same as StockpileInventoryDebugFragment.GiveAll
                        var stockpile = entity.GetComponent<Stockpile>();
                        var allower = entity.GetComponent<SingleGoodAllower>();
                        if (!(bool)stockpile || !(bool)allower || !allower.HasAllowedGood) break;
                        Inventory inventory = stockpile.Inventory;
                        string good = allower.AllowedGood;
                        inventory.GiveProduced(new GoodAmount(good, inventory.UnreservedCapacity(good)));
                        break;
                    }
                default:
                    Plugin.LogWarning($"Unknown dev entity action: {action}");
                    break;
            }
        }

        public override string ToActionString()
        {
            return $"Dev: {action} on {entityID}";
        }

        public static bool DoPrefix(BaseComponent component, string action, float delay = 0)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                string entityID = ReplayEvent.GetEntityID(component);
                if (entityID == null) return null;
                return new DevEntityActionEvent() { entityID = entityID, action = action, delay = delay };
            });
        }
    }

    [HarmonyPatch(typeof(ConstructionSiteDebugFragment), nameof(ConstructionSiteDebugFragment.OnFinishNowClick))]
    class ConstructionSiteDebugFragmentPatcher
    {
        static bool Prefix(ConstructionSiteDebugFragment __instance)
        {
            return DevEntityActionEvent.DoPrefix(
                (BaseComponent)(object)__instance._constructionSite, DevEntityActionEvent.FinishConstruction);
        }
    }

    [HarmonyPatch(typeof(WonderDebugFragment), nameof(WonderDebugFragment.OnProgressConstructionClick))]
    class WonderDebugFragmentPatcher
    {
        static bool Prefix(WonderDebugFragment __instance)
        {
            return DevEntityActionEvent.DoPrefix(
                (BaseComponent)(object)__instance._constructionSite, DevEntityActionEvent.ProgressWonder);
        }
    }

    [HarmonyPatch(typeof(DwellingDebugFragment), nameof(DwellingDebugFragment.SpawnNewborn))]
    class DwellingDebugFragmentPatcher
    {
        static bool Prefix(DwellingDebugFragment __instance)
        {
            return DevEntityActionEvent.DoPrefix(__instance._dwelling, DevEntityActionEvent.SpawnNewborn);
        }
    }

    [HarmonyPatch(typeof(DeteriorableDebugFragment), nameof(DeteriorableDebugFragment.Expire))]
    class DeteriorableDebugFragmentPatcher
    {
        static bool Prefix(DeteriorableDebugFragment __instance)
        {
            return DevEntityActionEvent.DoPrefix(
                (BaseComponent)(object)__instance._deteriorable, DevEntityActionEvent.ZeroDurability);
        }
    }

    [HarmonyPatch(typeof(UnstableCoreDebugFragment), nameof(UnstableCoreDebugFragment.DetonateSelected))]
    class UnstableCoreDetonatePatcher
    {
        static bool Prefix(UnstableCoreDebugFragment __instance)
        {
            // The delay comes from the keys held, which only this player knows
            var input = __instance._inputService;
            float delay = input.IsKeyHeld(UnstableCoreDebugFragment.DetonationDelayKey) ? 10f
                : input.IsKeyHeld(UnstableCoreDebugFragment.LongDetonationDelayKey) ? 20f
                : 0f;
            return DevEntityActionEvent.DoPrefix(__instance._unstableCore, DevEntityActionEvent.DetonateCore, delay);
        }
    }

    [HarmonyPatch(typeof(UnstableCoreDebugFragment), nameof(UnstableCoreDebugFragment.OnRemoveButtonClicked))]
    class UnstableCoreDeletePatcher
    {
        static bool Prefix(UnstableCoreDebugFragment __instance)
        {
            return DevEntityActionEvent.DoPrefix(__instance._unstableCore, DevEntityActionEvent.DeleteCore);
        }
    }

    [HarmonyPatch(typeof(StockpileInventoryDebugFragment), nameof(StockpileInventoryDebugFragment.GiveAll))]
    class StockpileInventoryDebugFragmentPatcher
    {
        static bool Prefix(StockpileInventoryDebugFragment __instance)
        {
            return DevEntityActionEvent.DoPrefix(__instance._stockpile, DevEntityActionEvent.FillStockpile);
        }
    }
}
