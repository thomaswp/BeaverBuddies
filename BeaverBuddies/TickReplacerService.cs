using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.RecoveredGoodSystem;
using Timberborn.TickSystem;
using Timberborn.WaterObjects;

namespace BeaverBuddies
{
    /// <summary>
    /// Service for adding ticking behaviors, e.g. that occur during an
    /// Update in the original code and had to be removed.
    /// </summary>
    internal class TickReplacerService : ITickableSingleton
    {
        private RecoveredGoodStackSpawner _goodStackSpawner;
        private WaterObjectService _waterObjectService;


        public TickReplacerService(
            RecoveredGoodStackSpawner spawner,
            WaterObjectService waterObjectService
        )
        {
            _goodStackSpawner = spawner;
            _waterObjectService = waterObjectService;
        }

        public void Tick()
        {
            // Move the normal update behavior to a tick
            RecoveredGoodStackSpawnerUpdateSingletonPatcher.BaseUpdateSingleton(_goodStackSpawner);
            WaterObjectServiceUpdateSingletonPatcher.BaseUpdateSingleton(_waterObjectService);
        }
    }
}
