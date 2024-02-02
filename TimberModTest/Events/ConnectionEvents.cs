using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Core;
using Timberborn.CoreUI;
using Timberborn.TimeSystem;

namespace TimberModTest.Events
{
    [Serializable]
    public class RandomStateSetEvent : ReplayEvent
    {
        public int seed;
        public int newTicksSinceLoad;
        public int entityUpdateHash;
        public int positionHash;

        public override void Replay(IReplayContext context)
        {
            UnityEngine.Random.InitState(seed);
            Plugin.Log($"Setting seed to {seed}; s0 = {UnityEngine.Random.state.s0}");

            if (context != null)
            {
                context.GetSingleton<ReplayService>().SetTicksSinceLoad(newTicksSinceLoad);
                TEBPatcher.SetHashes(entityUpdateHash, positionHash);
            }
        }

        public static RandomStateSetEvent CreateAndExecute(int ticksSinceLoad)
        {
            int seed = UnityEngine.Random.RandomRangeInt(int.MinValue, int.MaxValue);
            RandomStateSetEvent message = new RandomStateSetEvent()
            {
                seed = seed,
                newTicksSinceLoad = ticksSinceLoad,
                entityUpdateHash = TEBPatcher.EntityUpdateHash,
                positionHash = TEBPatcher.PositionHash,
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
            "The host should save and reload the save. " +
            "Once reloaded, the client should restart their game.\n" +
            "Before reloading, would you like to file a bug report to " +
            "help us fix this issue?";

        const string BUG_REPORT_URL = "https://github.com/thomaswp/TimberReplay/issues";

        public override void Replay(IReplayContext context)
        {
            context.GetSingleton<ReplayService>().SetTargetSpeed(0);
            var shower = context.GetSingleton<DialogBoxShower>();
            var urlOpener = context.GetSingleton<UrlOpener>();
            Action action = () =>
            {
                urlOpener.OpenUrl(BUG_REPORT_URL);
            };
            shower.Create().SetMessage(MESSAGE)
                .SetConfirmButton(action)
                .SetDefaultCancelButton()
                .Show();
        }
    }
}
