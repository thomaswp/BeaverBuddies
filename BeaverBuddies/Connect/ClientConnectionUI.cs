using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.MainMenuScene;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    [HarmonyPatch(typeof(MainMenuPanel), "GetPanel")]
    public class MainMenuGetPanelPatcher
    {
        public static void Postfix(ref VisualElement __result)
        {
            VisualElement root = __result.Query("MainMenuPanel");
            var previousButton = root.Children().ToList()[3] as LocalizableButton;
            var classList = previousButton.classList;
            Button button = new LocalizableButton();
            button.classList.AddRange(classList);
            button.text = "Join Co-op Game";
            button.clicked += ClientConnectionUI.OpenBox;
            root.Insert(4, button);
        }
    }

    public class ClientConnectionUI
    {
        private static ClientConnectionUI instance;
        private DialogBoxShower _dialogBoxShower;
        private InputBoxShower _inputBoxShower;
        private ClientConnectionService _clientConnectionService;

        public ClientConnectionUI(
            DialogBoxShower dialogBoxShower, 
            InputBoxShower inputBoxShower, 
            ClientConnectionService clientConnectionService
        ) 
        {
            instance = this;
            _dialogBoxShower = dialogBoxShower;
            _inputBoxShower = inputBoxShower;
            _clientConnectionService = clientConnectionService;
        }

        private void ShowBox()
        {
            var builder = _inputBoxShower.Create()
                .SetLocalizedMessage("Enter the global IP address of the Host:")
                .SetConfirmButton(ip =>
                {
                    EventIO.Config.ClientConnectionAddress = ip;
                    EventIO.Config.SaveConfig();
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
