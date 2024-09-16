using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WebNavigation;

namespace BeaverBuddies.Connect
{
    public class FirstTimerService : IPostLoadableSingleton
    {
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;
        private ConfigIOService _configIOService;

        internal FirstTimerService(
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            ConfigIOService configIOService
        )
        {
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
            _configIOService = configIOService;
        }

        // TODO: Ideally, wait to show until after OK is clicked.
        public void PostLoad()
        {
            var config = EventIO.Config;

            if (!config.FirstTimer)
            {
                return;
            }

            ILoc _loc = _dialogBoxShower._loc;

            var unsetFirstTimer = new Action(() =>
            {
                config.FirstTimer = false;
                _configIOService.SaveConfigToFile();
            });

            var action = () =>
            {
                unsetFirstTimer();
                _urlOpener.OpenUrl(LinkHelper.GuideURL);
            };

            _dialogBoxShower.Create()
                .SetMessage(_loc.T("BeaverBuddies.FirstTimer.Message"))
                .SetConfirmButton(action)
                .SetCancelButton(unsetFirstTimer)
                .Show();
        }
    }
}
