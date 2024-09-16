using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.MainMenuScene;
using Timberborn.Localization;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json.Bson;
using Timberborn.GameSceneLoading;
using BeaverBuddies.IO;

namespace BeaverBuddies.Connect
{

    [HarmonyPatch(typeof(LoadGameBox), nameof(LoadGameBox.GetPanel))]
    public class LoadGameBoxGetPanelPatcher
    {
        public static void Postfix(LoadGameBox __instance, ref VisualElement __result)
        {
            ILoc _loc = __instance._loc;
            ButtonInserter.DuplicateOrGetButton(__result, "LoadButton", "HostButton", (button) =>
            {
               button.text = _loc.T("BeaverBuddies.Saving.HostCoopGame");
               button.clicked += () => HostSelectedGame(__instance);
            });
        }

        [ManualMethodOverwrite]
        /*
         * 08/2024
        if (_saveList.TryGetSelectedSave(out var selectedSave))
        {
            if (_gameSaveRepository.SaveExists(selectedSave.SaveReference))
            {
                _validatingGameLoader.LoadGameIfSaveValid(selectedSave.SaveReference);
                return true;
            }

            Debug.LogWarning("Save: " + selectedSave.DisplayName + " doesn't exist, failed to load.");
        }
        return false;
         */
        // Duplicates the LoadGameBox.LoadGame method, but loads the game with
        // the HostingSaveReference instead of ValidatingGameLoader
        private static void HostSelectedGame(LoadGameBox __instance)
        {
            if (__instance._saveList.TryGetSelectedSave(out var selectedSave))
            {
                if (__instance._gameSaveRepository.SaveExists(selectedSave.SaveReference))
                {
                    ServerHostingUtils.LoadIfSaveValidAndHost(__instance._validatingGameLoader, selectedSave.SaveReference);
                }
                // Debug.LogWarning("Save: " + selectedSave.DisplayName + " doesn't exist, failed to load.");
            }
        }
    }

    internal class ServerHostingUtils
    {
        public static void LoadIfSaveValidAndHost(ValidatingGameLoader loader, SaveReference saveReferece)
        {
            CheckNextValidator(loader, saveReferece, 0);
        }

        [ManualMethodOverwrite]
        /*
9/14/2024
if (index >= _gameLoadValidators.Length)
{
	_gameSceneLoader.StartSaveGame(saveReference);
	return;
}
_gameLoadValidators[index].ValidateSave(saveReference, delegate
{
	CheckNextValidator(saveReference, index + 1);
});
         */
        private static void CheckNextValidator(ValidatingGameLoader loader, SaveReference saveReference, int index)
        {
            if (index >= loader._gameLoadValidators.Length)
            {
                LoadAndHost(loader, saveReference);
                return;
            }
            loader._gameLoadValidators[index].ValidateSave(saveReference, delegate
            {
                CheckNextValidator(loader, saveReference, index + 1);
            });
        }

        public static void LoadAndHost(ValidatingGameLoader loader, SaveReference saveReference)
        {
            var sceneLoader = loader._gameSceneLoader;
            var repository = sceneLoader._gameSaveRepository;
            var inputStream = repository.OpenSaveWithoutLogging(saveReference);
            byte[] data;
            using (var memoryStream = new MemoryStream())
            {
                inputStream.CopyTo(memoryStream);
                data = memoryStream.ToArray();
            }
            inputStream.Close();
            Plugin.Log($"Reading map with length {data.Length}");

            ServerEventIO io = new ServerEventIO();
            EventIO.Set(io);
            io.Start(data);

            // Make sure to set the RNG seed before loading the map
            // The client will do the same
            DeterminismService.InitGameStartState(data);

            sceneLoader.StartSaveGame(saveReference);
        }
    }
}
