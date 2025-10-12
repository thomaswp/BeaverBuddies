﻿using BeaverBuddies.Editor;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Buildings;
using Timberborn.GameSceneLoading;
using Timberborn.SceneLoading;

namespace BeaverBuddies.MultiStart
{
    public class StartBuildingsService : RegisteredSingleton
    {

        private readonly ISceneLoader _sceneLoader;

        public List<BuildingSpec> StartingBuildings { get; private set; } = new List<BuildingSpec>();
        public bool IsMultiStart => StartingBuildings.Count > 1;

        public StartBuildingsService(ISceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public void RegisterStartingBuilding(BuildingSpec building)
        {
            Plugin.Log($"Registering start building #{StartingBuildings.Count}");
            StartingBuildings.Add(building);
        }

        public int MaxStartLocations()
        {
            NewGameMode newGameMode = _sceneLoader.GetSceneParameters<GameSceneParameters>().NewGameConfiguration.NewGameMode;
            if (newGameMode is MultiplayerNewGameMode multiplayerNewGameMode)
            {
                return multiplayerNewGameMode.Players;
            }
            // This is just a max, so if not specified, we use infinity
            return StartingLocationPlayer.DEFAULT_MAX_STARTING_LOCS;
        }
    }
}
