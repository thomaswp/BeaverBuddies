using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.MainMenuPanels;
using Timberborn.OptionsGame;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    [HarmonyPatch(typeof(MainMenuPanel), "GetPanel")]
    public class MainMenuGetPanelPatcher
    {
        public static void Postfix(ref VisualElement __result)
        {
            ClientConnectionUI.DoPostfix(__result);
        }
    }

    [HarmonyPatch(typeof(GameOptionsBox), "GetPanel")]
    public class GameOptionsBoxGetPanelPatcher
    {
        public static void Postfix(ref VisualElement __result)
        {
            ClientConnectionUI.DoPostfix(__result);
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

        public static void DoPostfix(VisualElement __result)
        {
            ButtonInserter.DuplicateOrGetButton(__result, "LoadGameButton", "JoinButton", button =>
            {
                button.text = "Join co-op game";
                button.clicked += OpenBox;
            });
        }

        private void ShowBox()
        {
            var builder = _inputBoxShower.Create()
                .SetLocalizedMessage("Enter the global IP address of the Host:")
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
