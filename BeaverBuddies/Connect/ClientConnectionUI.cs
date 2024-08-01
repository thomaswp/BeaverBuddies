using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.MainMenuPanels;
using Timberborn.OptionsGame;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    [HarmonyPatch(typeof(MainMenuPanel), "GetPanel")]
    public class MainMenuGetPanelPatcher
    {
        public static void Postfix(MainMenuPanel __instance, ref VisualElement __result)
        {
            ClientConnectionUI.DoPostfix(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(GameOptionsBox), "GetPanel")]
    public class GameOptionsBoxGetPanelPatcher
    {
        public static void Postfix(MainMenuPanel __instance, ref VisualElement __result)
        {
            ClientConnectionUI.DoPostfix(__instance, __result);
        }
    }

    public class ClientConnectionUI
    {
        private static ClientConnectionUI instance;
        private DialogBoxShower _dialogBoxShower;
        private InputBoxShower _inputBoxShower;
        private ClientConnectionService _clientConnectionService;
        private ConfigIOService _configIOService;

        ClientConnectionUI(
            DialogBoxShower dialogBoxShower, 
            InputBoxShower inputBoxShower, 
            ClientConnectionService clientConnectionService,
            ConfigIOService configIOService
        ) 
        {
            instance = this;
            _dialogBoxShower = dialogBoxShower;
            _inputBoxShower = inputBoxShower;
            _clientConnectionService = clientConnectionService;
            _configIOService = configIOService;
        }

        public static void DoPostfix(MainMenuPanel __instance, VisualElement __result)
        {
            ILoc _loc = __instance._loc;
            Button button = ButtonInserter.DuplicateOrGetButton(__result, "LoadGameButton", "JoinButton", button =>
            {
                button.text = _loc.T("BeaverBuddies.Menu.JoinCoopGame");
                button.clicked += OpenBox;
                /*
                // Extract Dictionary to Player.log
                Dictionary<string, string> localization = ((Loc)_loc)._localization;
                Plugin.Log("List of all key for Localization BEGIN");
                foreach (var item in localization)
                {
                    var valueAsString = "{" + String.Join("},{", item.Value) + "}";
                    Plugin.Log($"{item.Key}=[{valueAsString}]");
                }
                Plugin.Log("List of all key for Localization END");
                */
            });
        }

        private void ShowBox()
        {
            ILoc _loc = _inputBoxShower._loc;
            var builder = _inputBoxShower.Create()
                .SetLocalizedMessage(_loc.T("BeaverBuddies.JoinCoopGame.EnterIp"))
                .SetConfirmButton(ip =>
                {
                    EventIO.Config.ClientConnectionAddress = ip;
                    _configIOService.SaveConfigToFile();
                    _clientConnectionService.ConnectOrShowFailureMessage(ip);
                });
            builder._input.value = EventIO.Config.ClientConnectionAddress;
            builder.Show();
        }

        public static void OpenBox()
        {
            instance?.ShowBox();
        }
    }
}
