using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.DistributionSystem;
using Timberborn.DistributionSystemBatchControl;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;
using Timberborn.GameDistrictsMigrationBatchControl;
using Timberborn.Goods;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.InputSystem.InputRemoting;

namespace TimberModTest.Events
{
    enum DistributorType
    { 
        Children,
        Adults,
        Bots,
        Unknown
    }

    static class DistributorUtils
    {
        public static DistributorType GetDistributorType(IDistributorTemplate distributorTemplate)
        {
            DistributorType type = distributorTemplate switch
            {
                ChildrenDistributorTemplate _ => DistributorType.Children,
                AdultsDistributorTemplate _ => DistributorType.Adults,
                BotsDistributorTemplate _ => DistributorType.Bots,
                _ => DistributorType.Unknown
            };
            if (type == DistributorType.Unknown)
            {
                Plugin.LogWarning($"Unknown distributor type: {distributorTemplate.GetType()}");
            }
            return type;
        }


        private static PopulationDistributorRetriever retreiver = 
            new PopulationDistributorRetriever();
        public static PopulationDistributor GetDistributor(DistributorType distributorType, DistrictCenter districtCenter)
        {
            var distributor = distributorType switch
            {
                DistributorType.Children => retreiver.GetPopulationDistributor<ChildrenDistributorTemplate>(districtCenter),
                DistributorType.Adults => retreiver.GetPopulationDistributor<AdultsDistributorTemplate>(districtCenter),
                DistributorType.Bots => retreiver.GetPopulationDistributor<BotsDistributorTemplate>(districtCenter),
                _ => null
            };
            if (distributor == null)
            {
                Plugin.LogWarning($"Unknown distributor type: {distributorType}");
            }
            return distributor;
        }
    }

    class ManualMigrationEvent : ReplayEvent
    {
        public string fromDistrictID;
        public string toDistrictID;
        public int amount;
        public DistributorType distributorType;

        public override void Replay(IReplayContext context)
        {
            var fromDistrictCenter = GetComponent<DistrictCenter>(context, fromDistrictID);
            var toDistrictCenter = GetComponent<DistrictCenter>(context, toDistrictID);
            if (fromDistrictCenter == null || toDistrictCenter == null) { return; }
            
            var distributor = DistributorUtils.GetDistributor(distributorType, fromDistrictCenter);
            if (distributor == null) 
            {
                return; 
            }

            int amount = Math.Min(this.amount, distributor.Current);
            if (amount > 0)
            {
                distributor.MigrateToAndCheckAutomaticMigration(toDistrictCenter, amount);
            }
        }

        public override string ToActionString()
        {
            return $"Migrating {amount} from {fromDistrictID} to {toDistrictID}";
        }
    }

    [HarmonyPatch(typeof(ManualMigrationPopulationRow), nameof(ManualMigrationPopulationRow.MigratePopulation))]
    class ManualMigrationPopulationRowMigratePatcher
    {
        static bool Prefix(ManualMigrationPopulationRow __instance, int amount)
        {
            amount = Math.Min(amount, __instance._populationDistributor.Current);
            if (amount <= 0) return true;
            return ReplayEvent.DoPrefix(() =>
            {
                DistributorType type = DistributorUtils.GetDistributorType(__instance._populationDistributor._distributorTemplate);
                if (type == DistributorType.Unknown)
                {
                    return null;
                }
                var fromDistrictID = ReplayEvent.GetEntityID(__instance._populationDistributor.DistrictCenter);
                var toDistrictID = ReplayEvent.GetEntityID(__instance._target);
                return new ManualMigrationEvent()
                {
                    fromDistrictID = fromDistrictID,
                    toDistrictID = toDistrictID,
                    amount = amount,
                    distributorType = type
                };
            });
        }
    }

    class SetDistrictMinimumPopulationEvent : ReplayEvent
    {
        public string districtEntityID;
        public int minimumPopulation;
        public DistributorType distributorType;

        public override void Replay(IReplayContext context)
        {
            DistrictCenter districtCenter = GetComponent<DistrictCenter>(context, districtEntityID);
            if (districtCenter == null) return;
            PopulationDistributor distributor = DistributorUtils.GetDistributor(distributorType, districtCenter);
            if (distributor == null) return;
            distributor.SetMinimumAndMigrate(minimumPopulation);
        }

        public override string ToActionString()
        {
            return $"Setting minimum population for {distributorType} for " +
                $"district {districtEntityID} to {minimumPopulation}";
        }
    }

    [HarmonyPatch(typeof(PopulationDistributor), nameof(PopulationDistributor.SetMinimumAndMigrate))]
    class PopulationDistributorSetMinimumAndMigratePatcher
    {
        static bool Prefix(PopulationDistributor __instance, int minimum)
        {
            return ReplayEvent.DoEntityPrefix(__instance.DistrictCenter, (entityID) =>
            {
                DistributorType type = DistributorUtils.GetDistributorType(__instance._distributorTemplate);
                if (type == DistributorType.Unknown)
                {
                    return null;
                }
                return new SetDistrictMinimumPopulationEvent()
                {
                    districtEntityID = entityID,
                    distributorType = type,
                    minimumPopulation = minimum,
                };
            });
        }
    }

    class SetDistrictMigrationToggledEvent : ReplayEvent
    {
        public string districtEntityID;
        public bool isImmigration;
        public bool allow;
        public DistributorType distributorType;

        public override void Replay(IReplayContext context)
        {
            DistrictCenter districtCenter = GetComponent<DistrictCenter>(context, districtEntityID);
            if (districtCenter == null) return;
            PopulationDistributor distributor = DistributorUtils.GetDistributor(distributorType, districtCenter);
            if (distributor == null) return;
            if (isImmigration)
            {
                if (distributor.AllowImmigration == allow) return;
                distributor.ToggleAllowImmigrationAndMigrate();
            }
            else
            {
                if (distributor.AllowEmigration == allow) return;
                distributor.ToggleAllowEmigrationAndMigrate();
            }
        }

        public override string ToActionString()
        {
            string migrationType = isImmigration ? "immigration" : "emigration";
            string allowedType = allow ? "allowed" : "disallowed";
            return $"Toggling {migrationType} to {allowedType} for {distributorType} for " +
                $"district {districtEntityID}";
        }

        public static bool DoPrefix(PopulationDistributor __instance, bool isImmigration, bool allow)
        {
            return DoEntityPrefix(__instance.DistrictCenter, (entityID) =>
            {
                DistributorType type = DistributorUtils.GetDistributorType(__instance._distributorTemplate);
                if (type == DistributorType.Unknown)
                {
                    return null;
                }
                return new SetDistrictMigrationToggledEvent()
                {
                    districtEntityID = entityID,
                    distributorType = type,
                    isImmigration = isImmigration,
                    allow = allow,
                };
            });
        }
    }

    [HarmonyPatch(typeof(PopulationDistributor), nameof(PopulationDistributor.ToggleAllowImmigrationAndMigrate))]
    class PopulationDistributorToggleAllowImmigrationAndMigratePatcher
    {
        static bool Prefix(PopulationDistributor __instance)
        {
            return SetDistrictMigrationToggledEvent.DoPrefix(__instance, true, !__instance.AllowImmigration);
        }
    }

    [HarmonyPatch(typeof(PopulationDistributor), nameof(PopulationDistributor.ToggleAllowEmigrationAndMigrate))]
    class PopulationDistributorToggleAllowEmigrationAndMigratePatcher
    {
        static bool Prefix(PopulationDistributor __instance)
        {
            return SetDistrictMigrationToggledEvent.DoPrefix(__instance, false, !__instance.AllowEmigration);
        }
    }


    // We need a reference to the district to replay events that happen to the
    // GoodDistributionSetting
    class GoodDistributionSettingWithDistrict : GoodDistributionSetting
    {
        public DistrictDistributionSetting DistrictSetting { get; private set; }

        public GoodDistributionSettingWithDistrict(GoodSpecification goodSpecification, DistrictDistributionSetting districtSetting) : base(goodSpecification)
        {
            this.DistrictSetting = districtSetting;
        }
    }

    [HarmonyPatch(typeof(DistrictDistributionSetting), nameof(DistrictDistributionSetting.AddGoodDistributionSetting))]
    class DistrictDistributionSettingAddPatcher
    {
        static void Prefix(DistrictDistributionSetting __instance, ref GoodDistributionSetting goodDistributionSetting)
        {
            // Replace the goodDistributionSetting with a new one that has a reference to the district
            var newSetting = new GoodDistributionSettingWithDistrict(goodDistributionSetting._goodSpecification, __instance);
            newSetting.ExportThreshold = goodDistributionSetting.ExportThreshold;
            newSetting.ImportOption = goodDistributionSetting.ImportOption;
            newSetting.LastImportTimestamp = goodDistributionSetting.LastImportTimestamp;
            goodDistributionSetting = newSetting;
        }
    }

    [Serializable]
    class GoodDistributionSettingChangedEvent : ReplayEvent
    {
        public string districtEntityID;
        public string goodID;
        public float threshold;
        public ImportOption importOption;

        public override void Replay(IReplayContext context)
        {
            var distributionSetting = GetComponent<DistrictDistributionSetting>(context, districtEntityID);
            if (distributionSetting == null) return;
            var goodSetting = distributionSetting.GetGoodDistributionSetting(goodID);
            if (goodSetting == null)
            {
                Plugin.LogWarning($"Could not find good {goodID} in district {districtEntityID}");
                return;
            }
            if (goodSetting.ExportThreshold != threshold)
            {
                goodSetting.SetExportThreshold(threshold);
            }
            if (goodSetting.ImportOption != importOption)
            {
                goodSetting.SetImportOption(importOption);
            }
        }

        public override string ToActionString()
        {
            return base.ToActionString();
        }

        public static bool DoPrefix(GoodDistributionSetting __instance, float exportThreshold, ImportOption importOption, bool typeWarning = true)
        {
            if (!(__instance is GoodDistributionSettingWithDistrict))
            {
                if (typeWarning)
                {
                    Plugin.LogWarning("GoddDistributionSetting without district!!");
                }
                return true;
            }
            var district = ((GoodDistributionSettingWithDistrict)__instance).DistrictSetting;
            return DoEntityPrefix(district, entityID =>
            {
                return new GoodDistributionSettingChangedEvent()
                {
                    districtEntityID = entityID,
                    goodID = __instance.GoodId,
                    threshold = exportThreshold,
                    importOption = importOption,
                };
            });
        }
    }

    // I believe that the "Set" methods here are only called from the UI, so this is
    // similar to intercepting the UI events directly. Loading and initialization don't
    // call these events.
    [HarmonyPatch(typeof(GoodDistributionSetting), nameof(GoodDistributionSetting.SetExportThreshold))]
    class GoodDistributionSettingSetImportThresholdPatcher
    {
        static bool Prefix(GoodDistributionSetting __instance, float exportThreshold)
        {
            if (__instance.ExportThreshold == exportThreshold) return true;
            return GoodDistributionSettingChangedEvent.DoPrefix(__instance, exportThreshold, __instance.ImportOption);
        }
    }
    [HarmonyPatch(typeof(GoodDistributionSetting), nameof(GoodDistributionSetting.SetImportOption))]
    class GoodDistributionSettingSetImportOptionPatcher
    {
        static bool Prefix(GoodDistributionSetting __instance, ImportOption importOption)
        {
            if (__instance.ImportOption == importOption) return true;
            return GoodDistributionSettingChangedEvent.DoPrefix(__instance, __instance.ExportThreshold, importOption);
        }
    }
    [HarmonyPatch(typeof(GoodDistributionSetting), nameof(GoodDistributionSetting.SetDefault))]
    class GoodDistributionSettingSetDefaultPatcher
    {
        static bool Prefix(GoodDistributionSetting __instance)
        {
            var exportThreshold = 0f;
            var importOption = ((!__instance._goodSpecification.ForceImport) ? ImportOption.Auto : ImportOption.Forced);
            if (__instance.ImportOption == importOption && __instance.ExportThreshold == exportThreshold) return true;
            return GoodDistributionSettingChangedEvent.DoPrefix(__instance, exportThreshold, importOption, false);
        }
    }
}
