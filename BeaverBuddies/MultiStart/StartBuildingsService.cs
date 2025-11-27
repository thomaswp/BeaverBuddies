using BeaverBuddies.Editor;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Buildings;
using Timberborn.GameSceneLoading;
using Timberborn.NewGameConfigurationSystem;
using Timberborn.SceneLoading;

namespace BeaverBuddies.MultiStart
{
    public class StartBuildingsService : RegisteredSingleton
    {

        private readonly ISceneLoader _sceneLoader;

        public List<Building> StartingBuildings { get; private set; } = new List<Building>();
        public bool IsMultiStart => StartingBuildings.Count > 1;

        public StartBuildingsService(ISceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public void RegisterStartingBuilding(Building building)
        {
            Plugin.Log($"Registering start building #{StartingBuildings.Count}");
            StartingBuildings.Add(building);
        }

        public int MaxStartLocations()
        {
            GameModeSpec newGameMode = _sceneLoader.GetSceneParameters<GameSceneParameters>().NewGameConfiguration.GameMode;
            if (newGameMode is MultiplayerNewGameModeSpec multiplayerNewGameMode)
            {
                return multiplayerNewGameMode.Players;
            }
            // This is just a max, so if not specified, we use infinity
            return StartingLocationPlayer.DEFAULT_MAX_STARTING_LOCS;
        }
    }
}
