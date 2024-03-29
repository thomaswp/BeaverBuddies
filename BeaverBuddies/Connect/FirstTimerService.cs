﻿using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Core;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;

namespace BeaverBuddies.Connect
{
    public class FirstTimerService : IPostLoadableSingleton
    {
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;

        private const string Message = 
            "It looks like this is your first time using BeaverBuddies. " +
            "We recommend taking a quick look through our guide to make sure " +
            "you are set up to play. Would you like to do that now?";
        private const string GuideURL = "https://github.com/thomaswp/BeaverBuddies/wiki/Installation-and-Running";

        public FirstTimerService(
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener
        )
        {
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
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
                config.SaveConfig();
            });

            var action = () =>
            {
                unsetFirstTimer();
                _urlOpener.OpenUrl(GuideURL);
            };

            _dialogBoxShower.Create()
                .SetMessage(Message)
                .SetConfirmButton(action)
                // TODO: This doesn't seem to work...
                .SetCancelButton(unsetFirstTimer)
                .SetDefaultCancelButton()
                .Show();
        }
    }
}
