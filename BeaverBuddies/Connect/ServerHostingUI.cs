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
            var button = ButtonInserter.DuplicateButton(__result.Q<Button>("LoadButton"));
            button.text = "Host Co-op Game";
            button.clicked += () => ServerHostingUI.LoadAndHost(__instance);
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
