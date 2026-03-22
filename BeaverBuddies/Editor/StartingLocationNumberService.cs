using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.EntitySystem;

namespace BeaverBuddies.Editor
{
    public class StartingLocationNumberService : RegisteredSingleton
    {
        private readonly EntityComponentRegistry _entityComponentRegistry;

        public StartingLocationNumberService(EntityComponentRegistry entityComponentRegistry)
        {
            _entityComponentRegistry = entityComponentRegistry;
        }

        private List<StartingLocationPlayer> GetStartingLocations()
        {
            return _entityComponentRegistry.GetEnabled<StartingLocationPlayer>().ToList();
        }

        public void ResetNumbering()
        {
            int index = 0;
            foreach (var loc in GetStartingLocations().OrderBy(loc => loc.PlayerIndex))
            {
                loc.PlayerIndex = index++;
            }
        }

        public int GetMaxPlayers()
        {
            return Math.Max(GetStartingLocations().Count, 1);
        }
    }
}
