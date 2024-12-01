using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using BeaverBuddies.Reporting;
using BeaverBuddies.Util;
using System;
using System.Threading.Tasks;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.Localization;
using Timberborn.Versioning;
using Timberborn.WebNavigation;
using UnityEngine.UIElements;

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
        public string desyncID;
        public string desyncTrace;

        public override void Replay(IReplayContext context)
        {
            context.GetSingleton<ReplayService>().SetTargetSpeed(0);
            ReportingService reportingService = context.GetSingleton<ReportingService>();
            RehostingService rehostingService = context.GetSingleton<RehostingService>();
            GameSaveRepository repository = context.GetSingleton<GameSaveRepository>();
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            var urlOpener = context.GetSingleton<UrlOpener>();
            string ioType = EventIO.Get()?.GetType().Name;
            Button infoButton = null;
            Action bugReportAction = () =>
            {
                infoButton?.SetEnabled(false);
                Action<Task<bool>> onPost = (success) =>
                {
                    if (infoButton == null) return;
                    if (success.Result)
                    {
                        // TODO: loc
                        infoButton.text = "Success!";
                    }
                    else
                    {
                        // TODO: loc
                        infoButton.text = "Report Failed :(";
                    }
                };

                if (!rehostingService.SaveRehostFile(saveReference =>
                {
                    // TODO: Is there any way to include the log data too?
                    byte[] mapBytes = ServerHostingUtils.GetMapBtyes(repository, saveReference);
                    reportingService.PostDesync(desyncID, desyncTrace, ioType, mapBytes).ContinueWith(onPost);
                }, true))
                {
                    _ = reportingService.PostDesync(desyncID, desyncTrace, ioType, null).ContinueWith(onPost);
                };
                
            };
            bool isHost = EventIO.Get() is ServerEventIO;
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
            DialogBox box = shower.Create().SetLocalizedMessage("BeaverBuddies.ClientDesynced.Message")
                .SetInfoButton(bugReportAction, _loc.T("BeaverBuddies.ClientDesynced.PostBugReportButton"))
                .SetConfirmButton(reconnectAction, reconnectText)
                .SetDefaultCancelButton()
                .Show();
            infoButton = box.GetPanel().Q<Button>("InfoButton");
        }
    }
}
