using BeaverBuddies.Util;
using System;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WebNavigation;

namespace BeaverBuddies.Help
{
    public class FirstTimerService : IPostLoadableSingleton
    {
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;
        private Settings _settings;

        internal FirstTimerService(
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            Settings settings
        )
        {
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
            _settings = settings;
        }

        public void PostLoad()
        {
            if (!Settings.ShouldShowFirstTimerMessage)
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
