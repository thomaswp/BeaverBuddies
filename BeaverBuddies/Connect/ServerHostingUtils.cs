using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.MainMenuScene;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json.Bson;

namespace BeaverBuddies.Connect
{

    [HarmonyPatch(typeof(LoadGameBox), nameof(LoadGameBox.GetPanel))]
    public class LoadGameBoxGetPanelPatcher
    {
        public static void Postfix(LoadGameBox __instance, ref VisualElement __result)
        {
            // TODO: This still happens multiple times.. need to fix
            ButtonInserter.DuplicateOrGetButton(__result, "LoadButton", "HostButton", (button) =>
            {
                button.text = "Host co-op game";
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
        private static HostingSaveReference ToHostingSaveReference(SaveReference saveReference)
        {
            return new HostingSaveReference(saveReference.SettlementName, saveReference.SaveName);
        }
        public static void LoadIfSaveValidAndHost(ValidatingGameLoader loader, SaveReference saveReferece)
        {
            loader.LoadGameIfSaveValid(ToHostingSaveReference(saveReferece));
        }

        public static void LoadAndHost(ValidatingGameLoader loader, SaveReference saveReferece)
        {
            loader._modCompatibleGameLoader.LoadGame(ToHostingSaveReference(saveReferece));
        }
    }


    [HarmonyPatch(typeof(ModCompatibleGameLoader), nameof(ModCompatibleGameLoader.LoadGame))]
    public class ModCompatibleGameLoaderLoadGamePatcher
    {
        public static void Prefix(ModCompatibleGameLoader __instance, SaveReference saveReference)
        {
            if (saveReference is HostingSaveReference)
            {
                var repository = __instance._gameSceneLoader._gameSaveRepository;
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
            }
        }
    }


}
