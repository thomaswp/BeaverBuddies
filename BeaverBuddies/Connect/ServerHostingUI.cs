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

    [HarmonyPatch(typeof(LoadGameBox), "GetPanel")]
    public class LoadGameBoxGetPanelPatcher
    {
        public static void Postfix(LoadGameBox __instance, ref VisualElement __result)
        {
            // TODO: This still happens multiple times.. need to fix
            ButtonInserter.DuplicateOrGetButton(__result, "LoadButton", "HostButton", (button) =>
            {
                button.text = "Host co-op game";
                button.clicked += () => ServerHostingUI.LoadAndHost(__instance);
            });
        }
    }

    internal class ServerHostingUI
    {
        public static bool IsHosting = false;

        public static void LoadAndHost(LoadGameBox __instance)
        {
            IsHosting = true;
            __instance.LoadGame();
            IsHosting = false;
        }

        public static void LoadAndHost(ValidatingGameLoader loader, SaveReference saveReferece)
        {
            IsHosting = true;
            loader.LoadGame(saveReferece);
            IsHosting = false;
        }
    }


    [HarmonyPatch(typeof(ValidatingGameLoader), nameof(ValidatingGameLoader.LoadGame))]
    public class ValidatingGameLoaderLoadGamePatcher
    {
        public static void Prefix(ValidatingGameLoader __instance, SaveReference saveReference)
        {
            if (ServerHostingUI.IsHosting)
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
