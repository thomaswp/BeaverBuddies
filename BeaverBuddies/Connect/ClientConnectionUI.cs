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
        private InputBoxShower _inputBoxShower;
        private ClientConnectionService _clientConnectionService;
        private ILoc _loc;
        private Settings _settings;

        public ClientConnectionUI(
            InputBoxShower inputBoxShower, 
            ClientConnectionService clientConnectionService,
            ILoc loc,
            Settings settings
        ) 
        {
            _inputBoxShower = inputBoxShower;
            _clientConnectionService = clientConnectionService;
            _loc = loc;
            _settings = settings;
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
            ILoc _loc = _inputBoxShower._loc;
            var builder = _inputBoxShower.Create()
                .SetLocalizedMessage(_loc.T("BeaverBuddies.JoinCoopGame.EnterIp"))
                .SetConfirmButton(ip =>
                {
                    _settings.ClientConnectionAddress.SetValue(ip);
                    _clientConnectionService.ConnectOrShowFailureMessage(ip);
                });

            builder.Show();

            // Override max length for IPv6 and host names
            builder._input.maxLength = 128;
            builder._input.value = _settings.ClientConnectionAddress.Value;
        }
    }
}
