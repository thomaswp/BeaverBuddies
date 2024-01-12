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
            PopulationDistributorRetriever retreiver = new PopulationDistributorRetriever();
            var distributor = distributorType switch
            {
                DistributorType.Children => retreiver.GetPopulationDistributor<ChildrenDistributorTemplate>(fromDistrictCenter),
                DistributorType.Adults => retreiver.GetPopulationDistributor<AdultsDistributorTemplate>(fromDistrictCenter),
                DistributorType.Bots => retreiver.GetPopulationDistributor<BotsDistributorTemplate>(fromDistrictCenter),
                _ => null
            };
            if (distributor == null) 
            {
                Plugin.LogWarning($"Unknown distributor type: {distributorType}");
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
                DistributorType type = __instance._populationDistributor._distributorTemplate switch
                {
                    ChildrenDistributorInitializer _ => DistributorType.Children,
                    AdultsDistributorTemplate _ => DistributorType.Adults,
                    BotsDistributorTemplate _ => DistributorType.Bots,
                    _ => DistributorType.Unknown
                };
                if (type == DistributorType.Unknown)
                {
                    Plugin.LogWarning($"Unknown distributor type: " +
                        $"{__instance._populationDistributor._distributorTemplate.GetType()}");
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

    //[Serializable]
    //class ExportThresholdChangedEvent : ReplayEvent
    //{
    //    public string districtEntityID;
    //    public string goodID;
    //    public float threshold;

    //    public override void Replay(IReplayContext context)
    //    {
    //        var distributionSetting = GetComponent<DistrictDistributionSetting>(context, districtEntityID);
    //        if (distributionSetting != null) return;
    //        var goodSetting = distributionSetting.GetGoodDistributionSetting(goodID);
    //        if (goodSetting != null)
    //        {
    //            Plugin.LogWarning($"Could not find good {goodID} in district {districtEntityID}");
    //            return;
    //        }
    //        goodSetting.SetExportThreshold(threshold);
    //    }

    //    public override string ToActionString()
    //    {
    //        return base.ToActionString();
    //    }
    //}

    // This isn't going to work because the slider does not have a reference to
    // the district.
    //[HarmonyPatch(typeof(ExportThresholdSlider), nameof(ExportThresholdSlider.OnSliderChanged))]
    //class ExportThresholdSliderOnSliderChangedPatcher
    //{
    //    static bool Prefix(ExportThresholdSlider __instance, ChangeEvent<float> evt)
    //    {
    //        return ReplayEvent.DoPrefix(() =>
    //        {
    //            var districtEntityID = ReplayEvent.GetEntityID(__instance._setting._goodSpecification.);
    //            var goodID = __instance._goodDistributionSetting.GoodID;
    //            return new ExportThresholdChangedEvent()
    //            {
    //                districtEntityID = districtEntityID,
    //                goodID = goodID,
    //                threshold = value
    //            };
    //        });
    //    }
    //}

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
            if (distributionSetting != null) return;
            var goodSetting = distributionSetting.GetGoodDistributionSetting(goodID);
            if (goodSetting != null)
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
    }

    [HarmonyPatch(typeof(DistrictDistributionSetting), nameof(DistrictDistributionSetting.AddGoodDistributionSetting))]
    class DistrictDistributionSettingAddPatcher
    {
        static bool Prefix(DistrictDistributionSetting __instance, GoodDistributionSetting goodDistributionSetting)
        {
            var districtEntityID = ReplayEvent.GetEntityID(__instance);
            var goodID = goodDistributionSetting?.GoodId;
            if (districtEntityID == null || goodID == null) return true;

            __instance._goodDistributionSettings.Add(goodDistributionSetting);
            goodDistributionSetting.SettingChanged += (sender, e) =>
            {
                // If not loaded, proceed as normal
                if (!ReplayService.IsLoaded)
                {
                    __instance.OnSettingChanged(sender, e);
                    return;
                };

                // Otherwise record the event as usual
                var message = new GoodDistributionSettingChangedEvent()
                {
                    districtEntityID = districtEntityID,
                    goodID = goodID,
                    threshold = goodDistributionSetting.ExportThreshold,
                    importOption = goodDistributionSetting.ImportOption,
                };
                Plugin.Log(message.ToActionString());
                ReplayService.RecordEvent(message);

                // Only raise the event and do its effects if we're supposed
                // to play this event
                if (!EventIO.ShouldPlayPatchedEvents) return;
                __instance.OnSettingChanged(sender, e);
            };
            return false;
        }
    }
}
