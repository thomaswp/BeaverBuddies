﻿using BeaverBuddies.Connect;
using System;
using System.Collections.Generic;
using System.Text;
using TimberApi;
using Timberborn.Autosaving;
using Timberborn.Core;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameSaveRuntimeSystemUI;
using Timberborn.SettlementNameSystem;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;

namespace BeaverBuddies.Events
{
    [Serializable]
    public class InitializeClientEvent : ReplayEvent
    {
        public int seed;
        public int newTicksSinceLoad;
        public int entityUpdateHash;
        public int positionHash;
        public string serverModVersion;
        public string serverGameVersion;

        public override void Replay(IReplayContext context)
        {
            UnityEngine.Random.InitState(seed);
            Plugin.Log($"Setting seed to {seed}; s0 = {UnityEngine.Random.state.s0}");

            if (context != null)
            {
                context.GetSingleton<ReplayService>().SetTicksSinceLoad(newTicksSinceLoad);
                TEBPatcher.SetHashes(entityUpdateHash, positionHash);

                string warningMessage = null;
                if (serverGameVersion != Versions.GameVersion.ToString())
                {
                    warningMessage = $"Warning! Server Timberborn version ({serverGameVersion}) does not match client Timberborn version ({Versions.GameVersion}).\n" +
                        $"Please ensure that you are running the same version of the game.";
                } else if (serverModVersion != Plugin.Version)
                {
                    warningMessage = $"Warning! Server mod version ({serverModVersion}) does not match client mod version ({Plugin.Version}).\n" +
                        $"Please ensure that you are running the same version of the {PluginInfo.PLUGIN_NAME} mod.";
                }
                if (warningMessage != null)
                {
                    Plugin.LogWarning(warningMessage);
                    context.GetSingleton<DialogBoxShower>().Create().SetMessage(warningMessage).Show();
                }
            }
        }

        public static InitializeClientEvent CreateAndExecute(int ticksSinceLoad)
        {
            int seed = UnityEngine.Random.RandomRangeInt(int.MinValue, int.MaxValue);
            InitializeClientEvent message = new InitializeClientEvent()
            {
                seed = seed,
                newTicksSinceLoad = ticksSinceLoad,
                entityUpdateHash = TEBPatcher.EntityUpdateHash,
                positionHash = TEBPatcher.PositionHash,
                serverModVersion = Plugin.Version,
                serverGameVersion = Versions.GameVersion.ToString(),
            };
            // TODO: Not certain if this is the right time, or if it should be enqueued
            message.Replay(null);
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
                        // TODO: Failure message
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
