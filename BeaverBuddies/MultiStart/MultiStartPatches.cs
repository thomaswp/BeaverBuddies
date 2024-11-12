using AsmResolver.PE.DotNet.Metadata;
using BeaverBuddies.Editor;
using BeaverBuddies.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.GameSceneLoading;
using Timberborn.GameStartup;
using Timberborn.MainMenuPanels;
using Timberborn.SceneLoading;
using Timberborn.SelectionSystem;
using Timberborn.StartingLocationSystem;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
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
            var startBuildingService = GetSingleton<StartBuildingsService>();

            var startingLocations = GetAllStartingLocations(__instance._startingLocationService);

			Plugin.Log($"Found {startingLocations.Count} starting locations");

			// If we don't have multiple starting locations, then use default behavior
			if (startingLocations.Count <= 1) return true;

			// Order the locations by player index
			startingLocations = startingLocations.OrderBy(
				sl => sl.GetComponentFast<StartingLocationPlayer>()?.PlayerIndex ?? 0
			).ToList();

			int count = 0;
			int maxStartingLocations = startBuildingService.MaxStartLocations();
			Plugin.Log($"Max players: {maxStartingLocations}; Map supports players: {startingLocations.Count}");

			// Initialize each starting location; not just the first
			foreach (var startingLocation in startingLocations)
			{
				__instance._startingBuildingSpawner.Place(startingLocation.Placement);
				// Register all start buildings
				startBuildingService.RegisterStartingBuilding(__instance._startingBuildingSpawner.StartingBuilding);


                // If we're playing with fewer players than the map can support, stop early
                count++;
                if (count >= maxStartingLocations) break;
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

	[HarmonyPatch(typeof(CustomNewGameModeController), nameof(CustomNewGameModeController.Initialize))]
	public class CustomNewGameModeControllerInitializePatcher
	{
        static T RecursiveClone<T>(T original) where T : VisualElement, new()
        {
            T clone = (T)Activator.CreateInstance(original.GetType());
			//Plugin.Log(clone.GetType().Name);
			//Plugin.Log("0");

            // Copy styles, classes, name, etc.
			if (original.styleSheetList != null)
            {
                foreach (var sheet in original.styleSheetList)
                {
                    clone.styleSheets.Add(sheet);
                }
            }
            //Plugin.Log("1");
            clone.name = original.name;
            clone.classList.AddRange(original.classList);
            //Plugin.Log("2");

			// Some primitive types have children that don't need to cloned
			if (clone is IntegerField) return clone;

            // Recursively clone children
            foreach (var child in original.Children())
            {
                clone.Add(RecursiveClone(child));
            }
            //Plugin.Log("3");

            return clone;
        }

		public const string playersFieldName = "Players";

        public static void Postfix(CustomNewGameModeController __instance, VisualElement root, NewGameMode defaultNewGameMode, Action newGameModeChangedCallback)
		{
			IntegerField playersField = root.Query<IntegerField>(playersFieldName);

            // Don't create multiple times!
            if (playersField == null)
			{

				string baseFieldName = "StartingWater";
				string indexFieldName = "StartingAdults";

				// Get the wrapper for the top field, and clone it
				var originalParent = root.Q<IntegerField>(baseFieldName).parent;
				var parent = RecursiveClone(originalParent);
				parent.name = "PlayersWrapper";

				// Add it right above this field
				var indexParent = root.Q<IntegerField>(indexFieldName).parent;
				int index = indexParent.parent.IndexOf(indexParent);
				originalParent.parent.Insert(index, parent);

				// Update the label text
				var label = parent.Q<Label>();

				label.text = RegisteredLocalizationService.T("BeaverBuddies.NewGame.MaxStartingLocations"); ;
				label.name = "PlayersLabel";
				label.style.unityFontStyleAndWeight = FontStyle.Bold;

                // Rename the number field
                playersField = parent.Q<IntegerField>(baseFieldName);
                playersField.name = playersFieldName;
            }

            // Initialize it with the right start value and max/min, etc.
            __instance.QInitializedIntField(
                root, playersFieldName, StartingLocationPlayer.DEFAULT_MAX_STARTING_LOCS, 1, StartingLocationPlayer.MAX_PLAYERS);
        }
	}

    [HarmonyPatch(typeof(CustomNewGameModeController), nameof(CustomNewGameModeController.GetNewGameMode))]
    public class CustomNewGameModeControllerGetNewGameModePatcher
	{
		public static void Postfix(CustomNewGameModeController __instance, ref NewGameMode __result)
		{
			string fieldName = CustomNewGameModeControllerInitializePatcher.playersFieldName;
            var playersField = __instance._integerFields.Where(x => x.name == fieldName).FirstOrDefault();
			if (playersField != null)
			{
				int maxStartingSpots = CustomNewGameModeController.GetInt(playersField);
				__result = new MultiplayerNewGameMode(__result, maxStartingSpots);
			}
		}
	}

	static class MultiplayerSettingsUpdater
	{
		public static bool IsMultiplayer(NewGameModePanel __instance)
		{
			return GetMetadata(__instance) != null;
		}

		public static MultiplayerMapMetadata GetMetadata(NewGameModePanel __instance)
		{
            var mapRef = __instance?._map?.MapFileReference;
            if (!mapRef.HasValue) return null;

            var metadata = GetSingleton<MultiplayerMapMetadataService>().TryGetMultiplayerMapMetadata(mapRef.Value);
			return metadata;
        }

        // This doesn't seem to work; update too late; too buggy to use
        // And isn't doing anything that important anyway...
        public static void MapOrDifficultyChanged(NewGameModePanel __instance)
        {
			var metadata = GetMetadata(__instance);

            string playersFieldName = CustomNewGameModeControllerInitializePatcher.playersFieldName;
            var playersField = __instance._root.Q<IntegerField>(playersFieldName);

            if (playersField == null) return;

            // Show/hide the field depending on if we have multiplayer metadata
			// Seems this has a delay to it
            playersField.parent.style.display = metadata == null ? DisplayStyle.None : DisplayStyle.Flex;

            if (metadata == null) return;

            Plugin.Log($"Map max players: {metadata.MaxPlayers}");
			int currentValue = CustomNewGameModeController.GetInt(playersField);
			if (currentValue > metadata.MaxPlayers)
			{
				playersField.value = metadata.MaxPlayers;
			}
        }
    }

    [HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.SelectModeButton))]
    public class NewGameModePanelSelectModeButtonPatcher
	{
		public static void Postfix(NewGameModePanel __instance, Button button, NewGameMode predefinedNewGameMode)
		{
            if (button == null || predefinedNewGameMode == null)
			{
				return;
			}

			if (MultiplayerSettingsUpdater.IsMultiplayer(__instance))
            {
                // Automatically open the customization panel, so they can select max starting locs
                __instance.OnCustomizeButtonClicked();

            }
        }
	}

	// Doesn't seem necessary
	//[HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.OnCustomizeButtonClicked))]
	//public class NewGameModePanelOnCustomizeButtonClickedPatcher
	//{
	//	public static void Postfix(NewGameModePanel __instance)
	//	{
 //       }
	//}

    [HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.SelectFactionAndMap))]
    public class NewGameModePanelSelectFactionAndMapPatcher
    {
        public static void Postfix(NewGameModePanel __instance)
        {
            if (MultiplayerSettingsUpdater.IsMultiplayer(__instance))
			{
				if (__instance._selectedModeButton != null)
				{
					__instance.OnCustomizeButtonClicked();
				}
			}
        }
    }
}
