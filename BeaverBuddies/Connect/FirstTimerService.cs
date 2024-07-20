using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using Timberborn.WebNavigation;

namespace BeaverBuddies.Connect
{
    public class FirstTimerService : IPostLoadableSingleton
    {
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;
        private ConfigIOService _configIOService;

        private const string Message = 
            "It looks like this is your first time using BeaverBuddies. " +
            "We recommend taking a quick look through our guide to make sure " +
            "you are set up to play. Would you like to do that now?";
        private const string GuideURL = "https://github.com/thomaswp/BeaverBuddies/wiki/Installation-and-Running";

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

            var unsetFirstTimer = new Action(() =>
            {
                config.FirstTimer = false;
                _configIOService.SaveConfigToFile();
            });

            var action = () =>
            {
                unsetFirstTimer();
                _urlOpener.OpenUrl(GuideURL);
            };

            _dialogBoxShower.Create()
                .SetMessage(Message)
                .SetConfirmButton(action)
                .SetCancelButton(unsetFirstTimer)
                .Show();
        }
    }
}
