﻿using BeaverBuddies.Connect;
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
        //public string mapName;
        public bool isDebugMode;

        public override void Replay(IReplayContext context)
        {
            //context.GetSingleton<ReplayService>().SetServerMapName(mapName);
            string warningMessage = null;
            if (serverGameVersion != GameVersions.CurrentVersion.ToString())
            {
                warningMessage = $"Warning! Server Timberborn version ({serverGameVersion}) does not match client Timberborn version ({GameVersions.CurrentVersion}).\n" +
                    $"Please ensure that you are running the same version of the game.";
            } else if (serverModVersion != Plugin.Version)
            {
                warningMessage = $"Warning! Server mod version ({serverModVersion}) does not match client mod version ({Plugin.Version}).\n" +
                    $"Please ensure that you are running the same version of the {Plugin.ID} mod.";
            } else if (isDebugMode != Settings.Debug)
            {
                // TODO: Should debug mode just come from the server?
                // Could be a bit tricky, since it must come before load
                warningMessage = $"Warning! Server debug mode ({isDebugMode}) does not match client debug mode ({Settings.Debug}).\n" +
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
                serverGameVersion = GameVersions.CurrentVersion.ToString(),
                isDebugMode = Settings.Debug,
                //mapName = mapName,
            };
            return message;
        }
    }

    [Serializable]
    public class ClientDesyncedEvent : ReplayEvent
    {
        public string desyncID;
        public string desyncTrace;

        private void ConfirmConsent(IReplayContext context, Action confirmCallback)
        {
            Settings settings = context.GetSingleton<Settings>();

            // If they've already consented, just skip the dialog
            if (settings.ReportingConsent.Value)
            {
                confirmCallback();
                return;
            }
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            shower.Create()
                .SetLocalizedMessage("BeaverBuddies.ClientDesynced.ConsentMessage")
                .SetConfirmButton(() =>
                {
                    // Save the consent to the config
                    settings.ReportingConsent.SetValue(true);
                    confirmCallback();
                }, _loc.T("BeaverBuddies.ClientDesynced.ConsentAgreement"))
                .SetDefaultCancelButton()
                .Show();
        }

        private void PostDesync(IReplayContext context, Action<string> callback)
        {
            ReplayService replayService = context.GetSingleton<ReplayService>();
            ReportingService reportingService = context.GetSingleton<ReportingService>();
            RehostingService rehostingService = context.GetSingleton<RehostingService>();
            GameSaveRepository repository = context.GetSingleton<GameSaveRepository>();
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            string ioType = EventIO.Get()?.GetType().Name;
            string mapName = replayService.ServerMapName;
            Action<Task<bool>> onPost = (success) =>
            {
                if (success.Result)
                {
                    callback("BeaverBuddies.ClientDesynced.ReportSuccess");
                }
                else
                {
                    callback("BeaverBuddies.ClientDesynced.ReportFailed");
                }
            };

            string versionInfo = $"BeaverBuddies: {Plugin.Version}; Timberborn: {GameVersions.CurrentVersion}";

            if (!rehostingService.SaveRehostFile(saveReference =>
            {
                byte[] mapBytes = ServerHostingUtils.GetMapBtyes(repository, saveReference);
                reportingService.PostDesync(desyncID, desyncTrace, ioType, mapName, versionInfo, mapBytes).ContinueWith(onPost);
            }, true))
            {
                _ = reportingService.PostDesync(desyncID, desyncTrace, ioType, mapName, versionInfo, null).ContinueWith(onPost);
            };
        }

        private void TurnOnTracing(Action<string> callback)
        {
            Settings.TemporarilyDebug = true;
            callback("BeaverBuddies.ClientDesynced.TracingEnabled");
        }

        public override void Replay(IReplayContext context)
        {
            ReplayService replayService = context.GetSingleton<ReplayService>();
            replayService.SetTargetSpeed(0);
            ReportingService reportingService = context.GetSingleton<ReportingService>();
            RehostingService rehostingService = context.GetSingleton<RehostingService>();
            GameSaveRepository repository = context.GetSingleton<GameSaveRepository>();
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            Button infoButton = null;

            Action<string> infoCallback = (message) =>
            {
                if (infoButton != null)
                {
                    infoButton.text = _loc.T(message);
                }
            };
            Action bugReportAction = () =>
            {
                if (Settings.Debug)
                {
                    ConfirmConsent(context, () =>
                    {
                        infoButton?.SetEnabled(false);
                        PostDesync(context, infoCallback);
                    });
                }
                else
                {
                    infoButton?.SetEnabled(false);
                    TurnOnTracing(infoCallback);
                }
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
            string reconnectMessage = _loc.T("BeaverBuddies.ClientDesynced.Message");
            string bugReportMessageKey;
            if (Settings.Debug)
            {
                bugReportMessageKey = "BeaverBuddies.ClientDesynced.PostBugReportButton";
            }
            else
            {
                reconnectMessage += "\n\n" + _loc.T("BeaverBuddies.ClientDesynced.NeedToEnableTracing");
                bugReportMessageKey = "BeaverBuddies.ClientDesynced.EnableTracing";
            }



            var builder = shower.Create().SetMessage(reconnectMessage);
            if (reportingService.HasAccessToken)
            {
                // Only show the bug report button if we have the ability to post it
                builder.SetInfoButton(bugReportAction, _loc.T(bugReportMessageKey));
            }
            DialogBox box = builder.SetConfirmButton(reconnectAction, reconnectText)
                .SetDefaultCancelButton()
                .Show();
            infoButton = box.GetPanel().Q<Button>("InfoButton");
        }
    }
}
