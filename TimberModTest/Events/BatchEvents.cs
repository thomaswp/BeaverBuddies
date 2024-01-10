using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;
using Timberborn.GameDistrictsMigrationBatchControl;
using static UnityEngine.GraphicsBuffer;

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

    // TODO: Don't disable!
    //[HarmonyPatch(typeof(ManualMigrationPopulationRow), nameof(ManualMigrationPopulationRow.MigratePopulation))]
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

}
