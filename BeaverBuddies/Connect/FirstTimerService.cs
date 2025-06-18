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
        private Settings _settings;

        internal FirstTimerService(
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            ConfigIOService configIOService,
            Settings settings
        )
        {
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
            _configIOService = configIOService;
            _settings = settings;
        }

        // TODO: Ideally, wait to show until after OK is clicked.
        public void PostLoad()
        {
            if (!_settings.ShowFirstTimerMessage.Value)
            {
                return;
            }

            ILoc _loc = _dialogBoxShower._loc;

            var unsetFirstTimer = new Action(() =>
            {
                _settings.ShowFirstTimerMessage.SetValue(false);
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
