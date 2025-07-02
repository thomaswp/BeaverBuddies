using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
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
        public static void Postfix(IPanelController __instance, ref VisualElement __result)
        {
            SingletonManager.GetSingleton<ClientConnectionUI>().AddJoinButton(__result);
        }
    }

    [HarmonyPatch(typeof(GameOptionsBox), "GetPanel")]
    public class GameOptionsBoxGetPanelPatcher
    {
        public static void Postfix(IPanelController __instance, ref VisualElement __result)
        {
            SingletonManager.GetSingleton<ClientConnectionUI>().AddJoinButton(__result);
        }
    }

    public class ClientConnectionUI : RegisteredSingleton
    {
        private DialogBoxShower _dialogBoxShower;
        private InputBoxShower _inputBoxShower;
        private ClientConnectionService _clientConnectionService;
        private ConfigIOService _configIOService;
        private ILoc _loc;

        ClientConnectionUI(
            DialogBoxShower dialogBoxShower, 
            InputBoxShower inputBoxShower, 
            ClientConnectionService clientConnectionService,
            ConfigIOService configIOService,
            ILoc loc
        ) 
        {
            _dialogBoxShower = dialogBoxShower;
            _inputBoxShower = inputBoxShower;
            _clientConnectionService = clientConnectionService;
            _configIOService = configIOService;
            _loc = loc;
        }

        public void AddJoinButton(VisualElement __result)
        {
            Button button = ButtonInserter.DuplicateOrGetButton(__result, "LoadGameButton", "JoinButton", button =>
            {
                button.text = _loc.T("BeaverBuddies.Menu.JoinCoopGame");
                button.clicked += () => ShowBox();
            });
        }

        private void ShowBox()
        {
            FieldInfo characterLimitField = typeof(InputBoxShower).GetField("CharacterLimit", BindingFlags.Static | BindingFlags.NonPublic);
            if (characterLimitField != null)
            {
                characterLimitField.SetValue(null, 100); 
            }

            ILoc _loc = _inputBoxShower._loc;
            var builder = _inputBoxShower.Create()
                .SetLocalizedMessage(_loc.T("BeaverBuddies.JoinCoopGame.EnterIp"))
                .SetConfirmButton(ip =>
                {
                    EventIO.Config.ClientConnectionAddress = ip;
                    _configIOService.SaveConfigToFile();
                    _clientConnectionService.ConnectOrShowFailureMessage(ip);
                });

            FieldInfo inputFieldInfo = typeof(InputBoxShower.Builder).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic);
            TextField inputField = inputFieldInfo?.GetValue(builder) as TextField;
            if (inputField != null)
            {
                inputField.maxLength = 100;
                inputField.value = EventIO.Config.ClientConnectionAddress; 
            }

            builder.Show();
        }
    }
}
