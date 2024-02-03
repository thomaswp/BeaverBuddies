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
            var previousButton = __result.Q<Button>("LoadButton");
            var classList = previousButton.classList;
            Button button = new LocalizableButton();
            button.classList.AddRange(classList);
            button.text = "Host Co-op Game";
            button.clicked += () => ServerHostingUI.LoadAndHost(__instance);

            var parent = previousButton.parent;
            var index = parent.IndexOf(previousButton);
            previousButton.parent.Insert(index + 1, button);
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
