using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private SingletonListener listener;

        internal FirstTimerService(
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            ConfigIOService configIOService,
            Settings settings,
            SingletonListener singletonListener
        )
        {
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
            _configIOService = configIOService;
            _settings = settings;
            listener = singletonListener;
        }

        // TODO: Ideally, wait to show until after OK is clicked.
        public void PostLoad()
        {
            foreach (var item in listener.Collect())
            {
                Plugin.Log(item.GetType().ToString());
            }
            var me = listener.Collect().OfType<FirstTimerService>().FirstOrDefault();
            Plugin.Log(this == me ? "Success!" : $"Me is {me}");

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
