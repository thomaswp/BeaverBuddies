using BeaverBuddies.Editor;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.GameSceneLoading;
using Timberborn.GameStartup;
using Timberborn.SceneLoading;
using Timberborn.SelectionSystem;
using Timberborn.StartingLocationSystem;
using UnityEngine;
using static BeaverBuddies.SingletonManager;
using static Timberborn.GameStartup.GameInitializer;

namespace BeaverBuddies.MultiStart
{
	[ManualMethodOverwrite]
	/*
     * 11/9/2024
		if (_startingLocationService.HasStartingLocation())
		{
			InitialPlacement = _startingLocationService.GetPlacement();
		}
		_startingBuildingSpawner.Place(InitialPlacement);
		RotateCamera();
		_startingLocationService.DeleteStartingLocations();
     */
	[HarmonyPatch(typeof(StartingBuildingInitializer), nameof(StartingBuildingInitializer.Initialize))]
	public class StartingBuildingInitializerInitializePatcher
	{
		public static bool Prefix(StartingBuildingInitializer __instance)
		{
			// TODO: Get number of active players from startup config
			var startingLocations = GetAllStartingLocations(__instance._startingLocationService);

			// TODO(BUG): Somehow when I have multiple starting locations they all get deleted
			// Would love to know what is causing that...
			Plugin.Log($"Found {startingLocations.Count} starting locations");

			// If we don't have multiple starting locations, then use default behavior
			if (startingLocations.Count <= 1) return true;

			// Order the locations by player index
			startingLocations = startingLocations.OrderBy(
				sl => sl.GetComponentFast<StartingLocationPlayer>()?.PlayerIndex ?? 0
			).ToList();

			var startBuildingService = GetSingleton<StartBuildingsService>();

			// Initialize each starting location; not just the first
			foreach (var startingLocation in startingLocations)
			{
				__instance._startingBuildingSpawner.Place(startingLocation.Placement);
				// Register all start buildings
				startBuildingService.RegisterStartingBuilding(__instance._startingBuildingSpawner.StartingBuilding);
			}

			// Center on the first spawn location
			__instance._startingBuildingSpawner._cameraTargeter
				.CenterCameraOn(startingLocations[0].GetComponentFast<SelectableObject>());
			__instance.RotateCamera();

			// We still delete all starting locations
			__instance._startingLocationService.DeleteStartingLocations();

			return false;
		}

		public static List<StartingLocation> GetAllStartingLocations(StartingLocationService sls)
		{
			return sls._entityComponentRegistry.GetEnabled<StartingLocation>().ToList();
		}
	}

	[ManualMethodOverwrite]
    /*
     * 11/9/2024
		Building startingBuilding = _startingBuildingSpawner.StartingBuilding;
		if ((bool)startingBuilding)
		{
			Vector3? unblockedSingleAccess = startingBuilding.GetComponentFast<BuildingAccessible>().Accessible.UnblockedSingleAccess;
			if (unblockedSingleAccess.HasValue)
			{
				Vector3 valueOrDefault = unblockedSingleAccess.GetValueOrDefault();
				NewGameMode newGameMode = _sceneLoader.GetSceneParameters<GameSceneParameters>().NewGameConfiguration.NewGameMode;
				_startingBeaverInitializer.Initialize(valueOrDefault, newGameMode.StartingAdults, newGameMode.AdultAgeProgress, newGameMode.StartingChildren, newGameMode.ChildAgeProgress);
			}
		}
		return InitializationState.UpdateStats;
     */
    [HarmonyPatch(typeof(GameInitializer), nameof(GameInitializer.SpawnBeavers))]
	public class GameInitializerSpawnBeaversPatcher
	{
		public static bool Prefix(GameInitializer __instance, ref InitializationState __result)
		{
            StartBuildingsService startBuildingsService = GetSingleton<StartBuildingsService>();
			if (!startBuildingsService.IsMultiStart) return true;

            foreach (Building startingBuilding in startBuildingsService.StartingBuildings)
            {
                Vector3? unblockedSingleAccess = startingBuilding.GetComponentFast<BuildingAccessible>().Accessible.UnblockedSingleAccess;
                if (unblockedSingleAccess.HasValue)
                {
                    Vector3 valueOrDefault = unblockedSingleAccess.GetValueOrDefault();
                    NewGameMode newGameMode = __instance._sceneLoader.GetSceneParameters<GameSceneParameters>().NewGameConfiguration.NewGameMode;
                    __instance._startingBeaverInitializer.Initialize(valueOrDefault, newGameMode.StartingAdults, newGameMode.AdultAgeProgress, newGameMode.StartingChildren, newGameMode.ChildAgeProgress);
                }
            }
            __result = InitializationState.UpdateStats;
			return false;
        }
	}
}
