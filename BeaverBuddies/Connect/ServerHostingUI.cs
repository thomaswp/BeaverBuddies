using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TimberApi.UiBuilderSystem.CustomElements;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.MainMenuScene;
using UnityEngine.UIElements;

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

        public static void LoadAndHost(LoadGameBox __instance)
        {
            EventIO.Set(new ServerEventIO());
            __instance.LoadGame();
        }
    }
}
