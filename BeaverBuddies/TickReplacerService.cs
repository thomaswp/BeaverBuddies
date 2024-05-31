using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.RecoveredGoodSystem;
using Timberborn.TickSystem;

namespace BeaverBuddies
{
    /// <summary>
    /// Service for adding ticking behaviors, e.g. that occur during an
    /// Update in the original code and had to be removed.
    /// </summary>
    internal class TickReplacerService : ITickableSingleton
    {
        private RecoveredGoodStackSpawner _spawner;

        public TickReplacerService(RecoveredGoodStackSpawner spawner)
        {
            _spawner = spawner;
        }

        public void Tick()
        {
            // Move the normal update behavior to a tick
            RecoveredGoodStackSpawnerUpdateSingletonPatcher.BaseUpdateSingleton(_spawner);
        }
    }
}
