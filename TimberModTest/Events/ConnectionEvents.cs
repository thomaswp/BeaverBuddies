using System;
using System.Collections.Generic;
using System.Text;

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
}
