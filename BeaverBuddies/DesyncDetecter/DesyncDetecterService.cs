using BeaverBuddies.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Timberborn.Common;

namespace BeaverBuddies.DesyncDetecter
{
    [Serializable]
    public struct Trace
    {
        public string message;
        public string stackTrace;
    }

    [Serializable]
    public class TraceLoggedForTickEvent : ReplayEvent
    {
        // tick refers to the tick for which these traces are captures
        // while ReplayEvent.ticksSinceLoad is the timing of when the
        // event was actually sent, which is usually 1 tick later
        public int tick;
        public List<Trace> traces;

        // This should only occur on the client side, and the
        // event should only be sent (manually) on the Server side
        public override void Replay(IReplayContext context)
        {
            if (!DesyncDetecterService.VerifyTraces(tick, traces))
            {
                var replayService = context.GetSingleton<ReplayService>();
                replayService.HandleDesync();
            }
        }

    }

    public class DesyncDetecterService : RegisteredSingleton, IResettableSingleton
    {
        private static int currentTick;
        private static readonly List<List<Trace>> traces = new List<List<Trace>>();
        private static List<Trace> CurrentTrace { get { return traces.Last(); } }

        // TODO: Get from Config
        private static readonly int maxTraceTicks = 10;

        DesyncDetecterService()
        {
            Reset();
        }

        public static List<string> GetCurrentTrace()
        {
            return CurrentTrace.Select(t => t.message).ToList();
        }

        public void Reset()
        {
            currentTick = -1;
            traces.Clear();
            traces.Add(new List<Trace>());
            Trace("Start Preload");
        }

        public static IEnumerable<ReplayEvent> CreateReplayEventsAndClear()
        {
            // The first tick is the current tick shifted by the number of traces - 1
            int tick = currentTick - (traces.Count - 1);
            while (traces.Count > 0)
            {
                Plugin.Log($"Sending {tick}: {traces[0].FirstOrDefault().message}");
                yield return new TraceLoggedForTickEvent()
                {
                    tick = tick,
                    traces = traces[0],
                };
                traces.RemoveAt(0);
                tick++;
            }
        }

        public static void StartTick(int tick)
        {
            if (tick < currentTick)
            {
                Plugin.LogError($"Ticks cannot decrease! {tick} < {currentTick}");
                // This shouldn't happen, but the best we can do is clear the
                // current traces and start over
                currentTick = tick - 1;
                traces.Clear();
            }
            // Each tick should be called, but if not
            // ensure that the list increments one at a time
            while (currentTick < tick)
            {
                currentTick++;
                traces.Add(new List<Trace>());
                Trace($"Tick {tick} started");
            }
        }

        // TODO: Could is there an efficient way to avoid string
        // building for high-frequency calls if this is disabled?
        // Probably better to just avoid the patches altogether
        public static void Trace(string message)
        {
            // TODO: Only if Config says so
            CurrentTrace.Add(new Trace()
            {
                message = message,
                // TODO: Only if Config says so
                stackTrace = new StackTrace().ToString(),
            });
        }

        public static bool VerifyTraces(int tick, List<Trace> otherTraces)
        {
            if (tick > currentTick)
            {
                Plugin.LogError($"Verifying future tick! {tick} > {currentTick}");
                return false;
            }


            // The tick we're looking for is the last one
            // minus the difference between the requested and current tick
            int index = traces.Count - 1 + tick - currentTick;
            if (index < 0)
            {
                Plugin.LogWarning($"Attempting to verify already deleted tick {tick}");
                return true;
            }

            List<Trace> myTraces = traces[index];

            int errorIndex = -1;
            for (int i = 0; i < myTraces.Count || i < otherTraces.Count; i++)
            {
                if (i >= myTraces.Count || i >= otherTraces.Count)
                {
                    errorIndex = i;
                    break;
                }
                if (myTraces[i].message != otherTraces[i].message)
                {
                    errorIndex = i;
                    break;
                }
            }
            if (errorIndex == -1)
            {
                // If the index we're verifying (that's ticked on both Client and Server)
                // is greater than the number of ticks we should keep as history (in case
                // of future desyncs), detele that many stored logs.
                // Note: This could fail with multiple clients, if one is way
                // behind the other, but that will just throw a warning and assume
                // no desync.
                int toDelete = index - maxTraceTicks;
                while (toDelete-- > 0)
                {
                    traces.RemoveAt(0);
                }

                return true;
            }

            Plugin.LogError($"Desync detected for tick {tick}!");
            Plugin.Log("========== Trace history ==========");
            for (int i = 0; i < index; i++)
            {
                LogTraces(i);
            }
            Plugin.Log($"========== Shared History for Desynced Tick {tick} ==========");
            for (int i = 0; i < errorIndex; i++)
            {
                LogTrace(myTraces[i], true);
            }
            Plugin.Log("========== Desynced Trace ==========");
            Plugin.Log("---------- My Trace ----------");
            if (errorIndex >= myTraces.Count)
            {
                Plugin.Log("No trace (index out of bounds)");
            }
            else
            {
                LogTrace(myTraces[errorIndex], true);
            }
            Plugin.Log("---------- Other Trace ----------");
            if (errorIndex >= otherTraces.Count)
            {
                Plugin.Log("No trace (index out of bounds)");
            }
            else
            {
                LogTrace(otherTraces[errorIndex], true);
            }
            Plugin.Log("========== Desynced Log End ==========");

            return false;
        }

        private static void LogTraces(int index)
        {
            traces[index].ForEach(t => LogTrace(t));
            Plugin.Log("----------------------------------");
        }

        private static void LogTrace(Trace trace, bool withStack = false)
        {
            string stack = withStack ? trace.stackTrace.ToString() : null;
            LogTrace(trace.message, stack);
        }

        private static void LogTrace(string message, string stack)
        {
            Plugin.Log(message);
            if (stack != null)
            {
                Plugin.Log(stack);
                Plugin.Log("--------------");
            }
        }
    }
}
