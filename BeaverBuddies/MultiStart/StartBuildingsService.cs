using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Buildings;

namespace BeaverBuddies.MultiStart
{
    public class StartBuildingsService : RegisteredSingleton
    {
        public List<Building> StartingBuildings { get; private set; } = new List<Building>();
        public bool IsMultiStart => StartingBuildings.Count > 1;

        public void RegisterStartingBuilding(Building building)
        {
            Plugin.Log($"Registering start building #{StartingBuildings.Count}");
            StartingBuildings.Add(building);
        }
    }
}
