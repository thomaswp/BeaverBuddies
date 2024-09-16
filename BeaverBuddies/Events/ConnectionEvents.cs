using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.Versioning;
using Timberborn.WebNavigation;

namespace BeaverBuddies.Events
{
    [Serializable]
    public class InitializeClientEvent : ReplayEvent
    {
        public string serverModVersion;
        public string serverGameVersion;
        public bool isDebugMode;

        public override void Replay(IReplayContext context)
        {
            string warningMessage = null;
            if (serverGameVersion != Versions.CurrentGameVersion.ToString())
            {
                warningMessage = $"Warning! Server Timberborn version ({serverGameVersion}) does not match client Timberborn version ({Versions.CurrentGameVersion}).\n" +
                    $"Please ensure that you are running the same version of the game.";
            } else if (serverModVersion != Plugin.Version)
            {
                warningMessage = $"Warning! Server mod version ({serverModVersion}) does not match client mod version ({Plugin.Version}).\n" +
                    $"Please ensure that you are running the same version of the {Plugin.ID} mod.";
            } else if (isDebugMode != EventIO.Config.Debug)
            {
                warningMessage = $"Warning! Server debug mode ({isDebugMode}) does not match client debug mode ({EventIO.Config.Debug}).\n" +
                    $"Please update your config files to be in or not in debug mode.";
            }
            if (warningMessage != null)
            {
                Plugin.LogWarning(warningMessage);
                context.GetSingleton<DialogBoxShower>().Create().SetMessage(warningMessage).Show();
            }
        }

        public static InitializeClientEvent Create()
        {
            InitializeClientEvent message = new InitializeClientEvent()
            {
                serverModVersion = Plugin.Version,
                serverGameVersion = Versions.CurrentGameVersion.ToString(),
                isDebugMode = EventIO.Config.Debug,
            };
            return message;
        }
    }

    [Serializable]
    public class ClientDesyncedEvent : ReplayEvent
    {
        public override void Replay(IReplayContext context)
        {
            context.GetSingleton<ReplayService>().SetTargetSpeed(0);
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            var urlOpener = context.GetSingleton<UrlOpener>();
            Action bugReportAction = () =>
            {
                urlOpener.OpenUrl(LinkHelper.BugReportURL);
            };
            bool isHost = EventIO.Get() is ServerEventIO;
            RehostingService rehostingService = context.GetSingleton<RehostingService>();
            Action reconnectAction = () =>
            {
                if (isHost)
                {
                    if (!rehostingService.RehostGame())
                    {
                        shower.Create()
                            .SetLocalizedMessage("BeaverBuddies.ClientDesynced.FailedToRehostMessage")
                            .Show();
                    }
                }
                else
                {
                    context.GetSingleton<ClientConnectionService>()
                    ?.ConnectOrShowFailureMessage();
                }
            };
            string reconnectText = isHost ? _loc.T("BeaverBuddies.ClientDesynced.SaveAndRehostButton") : _loc.T("BeaverBuddies.ClientDesynced.WaitForRehostButton");
            shower.Create().SetLocalizedMessage("BeaverBuddies.ClientDesynced.Message")
                .SetInfoButton(bugReportAction, _loc.T("BeaverBuddies.ClientDesynced.PostBugReportButton"))
                .SetConfirmButton(reconnectAction, reconnectText)
                .SetDefaultCancelButton()
                .Show();
        }
    }
}
