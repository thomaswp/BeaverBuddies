using BeaverBuddies.Connect;
using System;
using Timberborn.CoreUI;
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
        const string MESSAGE = 
            "A connected player desynced and cannot continue playing - sorry about that!\n" +
            "The Host should hit Rehost and, AFTER that, the Client should Reconnect.\n" +
            "You may need to restart the game if problems persist.\n" +
            "Before reloading, would you like to file a bug report to " +
            "help us fix this issue?";

        const string BUG_REPORT_URL = "https://github.com/thomaswp/TimberReplay/issues";

        public override void Replay(IReplayContext context)
        {
            context.GetSingleton<ReplayService>().SetTargetSpeed(0);
            var shower = context.GetSingleton<DialogBoxShower>();
            var urlOpener = context.GetSingleton<UrlOpener>();
            Action bugReportAction = () =>
            {
                urlOpener.OpenUrl(BUG_REPORT_URL);
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
                            .SetMessage("Failed to rehost. Manually save and host again.")
                            .Show();
                    }
                }
                else
                {
                    context.GetSingleton<ClientConnectionService>()
                    ?.ConnectOrShowFailureMessage();
                }
            };
            string reconnectText = isHost ? "Save and Rehost" : "Reconnect (wait for Rehost)";
            shower.Create().SetMessage(MESSAGE)
                .SetInfoButton(bugReportAction, "Post Bug Report")
                .SetConfirmButton(reconnectAction, reconnectText)
                .SetDefaultCancelButton()
                .Show();
        }
    }
}
