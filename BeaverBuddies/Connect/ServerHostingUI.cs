using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TimberApi.UiBuilderSystem.CustomElements;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.MainMenuScene;
using UnityEngine.UIElements;
using System.Threading.Tasks;

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

            //EventIO.Set(new ServerEventIO());
        }
    }

    [HarmonyPatch(typeof(ValidatingGameLoader), nameof(ValidatingGameLoader.LoadGame)]
    public class ValidatingGameLoaderLoadGamePatcher
    {
        public static void Prefix(ValidatingGameLoader __instance, SaveReference saveReference)
        {
            if (ServerHostingUI.IsHosting)
            {
                var repository = __instance._gameSceneLoader._gameSaveRepository;
                var inputStream = repository.OpenSaveWithoutLogging(saveReference);
                byte[] data = new byte[inputStream.Length];
                inputStream.Read(data, 0, data.Length);
                inputStream.Close();
                ServerEventIO io = new ServerEventIO();
                // We start with a static IO that starts at tick 0 and with the
                // loaded map to allow clients to join as the host loads.
                // Later we replace this with something more dynamic.
                io.Start(
                    EventIO.Config.Port, 
                    () => new Task<byte[]>(() => data), 
                    () => 0
                );
                EventIO.Set(io);
            }
        }
    }


}
