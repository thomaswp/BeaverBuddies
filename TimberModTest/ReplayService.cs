using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.PlantingUI;
using Timberborn.PrefabSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using TimberNet;
using UnityEngine;
using static Timberborn.TickSystem.TickableSingletonService;

namespace TimberModTest
{
    public interface IEarlyTickableSingleton : ITickableSingleton
    {
    }

    /**
     * Represents a group of events that should be sent
     * and received together, to ensure all events for a tick
     * are present before they are played.
     */
    class GroupedEvent : ReplayEvent
    {
        public List<ReplayEvent> events;

        public GroupedEvent(List<ReplayEvent> events)
        {
            // Make a copy
            this.events = events.ToList();
        }

        public override void Replay(IReplayContext context)
        {
            throw new NotImplementedException("Do not directly replay grouped events");
        }
    }

    class HeartbeatEvent : ReplayEvent
    {
        public override void Replay(IReplayContext context)
        {
            // No op
        }
    }

    public class ReplayService : IReplayContext, IPostLoadableSingleton, IUpdatableSingleton, IEarlyTickableSingleton
    {
        //private readonly TickWathcerService _tickWathcerService;
        private readonly EventBus _eventBus;
        private readonly SpeedManager _speedManager;
        private readonly GameSaver _gameSaver;

        private readonly GameSaveHelper gameSaveHelper;

        private List<object> singletons = new();

        private EventIO io => EventIO.Get();

        private int ticksSinceLoad = 0;

        public static bool ShouldInterruptTicking { get; private set; } = false;
        public int TargetSpeed  { get; private set; } = 0;

        private static ConcurrentQueue<ReplayEvent> eventsToSend = new ConcurrentQueue<ReplayEvent>();
        private static ConcurrentQueue<ReplayEvent> eventsToPlay = new ConcurrentQueue<ReplayEvent>();

        public static bool IsLoaded { get; private set; } = false;

        public static bool IsReplayingEvents { get; private set; } = false;

        public ReplayService(
            EventBus eventBus,
            SpeedManager speedManager,
            GameSaver gameSaver,
            BlockObjectPlacerService blockObjectPlacerService,
            BuildingService buildingService,
            PlantingSelectionService plantingSelectionService,
            TreeCuttingArea treeCuttingArea
        )
        {
            //_tickWathcerService = AddSingleton(tickWathcerService);
            _eventBus = AddSingleton(eventBus);
            _speedManager = AddSingleton(speedManager);
            _gameSaver = AddSingleton(gameSaver);
            AddSingleton(blockObjectPlacerService);
            AddSingleton(buildingService);
            AddSingleton(plantingSelectionService);
            AddSingleton(treeCuttingArea);

            // TODO: I think there's a SingletonRegistry that may
            // be able to do this.
            AddSingleton(this);

            _eventBus.Register(this);

            gameSaveHelper = new GameSaveHelper(gameSaver);

            //io = new FileWriteIO("test.json");
            //io = new FileReadIO("planting.json");
            //io = new FileReadIO("trees.json");
        }

        public void PostLoad()
        {
            Plugin.Log("PostLoad");
            DeterminismController.UnityThread = Thread.CurrentThread;
        }

        private T AddSingleton<T>(T singleton)
        {
            this.singletons.Add(singleton);
            return singleton;
        }

        public T GetSingleton<T>()
        {
            foreach (object singleton in singletons)
            {
                if (singleton is T)
                    return (T)singleton;
            }
            return default;
        }

        public static void RecordEvent(ReplayEvent replayEvent)
        {
            // During a replay, we save things manually, only if they're
            // successful.
            if (IsReplayingEvents) return;

            UserEventBehavior behavior = UserEventBehavior.Send;
            EventIO io = EventIO.Get();
            if (io != null)
            {
                behavior = io.UserEventBehavior;
            }
            if (behavior == UserEventBehavior.QueuePlay)
            {
                eventsToPlay.Enqueue(replayEvent);
            }
            else
            {
                EnqueueEventForSending(replayEvent);
            }
        }

        private void ReplayEvents()
        {
            if (TickableBucketServiceTick1Patcher.currentBucket != 0)
            {
                Plugin.LogWarning($"Warning, replaying events when bucket != 0: {TickableBucketServiceTick1Patcher.currentBucket}");
            }

            List<ReplayEvent> eventsToReplay = io.ReadEvents(ticksSinceLoad);
            // Spread grouped events into a flat list because we need to
            // replay each individually (since we only record them if successful).
            eventsToReplay = eventsToReplay
                .SelectMany(e => e is GroupedEvent grouped ? grouped.events.ToArray() : new ReplayEvent[] { e })
                .ToList();
            while (eventsToPlay.TryDequeue(out ReplayEvent replayEvent))
            {
                replayEvent.ticksSinceLoad = ticksSinceLoad;
                eventsToReplay.Add(replayEvent);
            }

            int currentTick = ticksSinceLoad;
            IsReplayingEvents = true;
            for (int i = 0; i < eventsToReplay.Count; i++)
            {
                ReplayEvent replayEvent = eventsToReplay[i];
                int eventTime = replayEvent.ticksSinceLoad;
                if (eventTime > currentTick)
                    break;
                if (eventTime < currentTick)
                {
                    Plugin.LogWarning($"Event past time: {eventTime} < {currentTick}");
                }
                //Plugin.Log($"Replaying event [{replayEvent.ticksSinceLoad}]: {replayEvent.type}");
                
                // If this event was played (e.g. on the server) and recorded a 
                // random state, make sure we're in the same state.
                if (replayEvent.randomS0Before != null)
                {
                    int s0 = UnityEngine.Random.state.s0;
                    if (s0 != replayEvent.randomS0Before)
                    {
                        Plugin.LogWarning($"Random state mismatch: {s0} != {replayEvent.randomS0Before}");
                        SetTargetSpeed(0);
                        SpeedChangePatcher.SetSpeedSilently(_speedManager, 0);
                        //DeterminismController.PrintRandomStacks();
                        break;
                        // TODO: Resync!
                    }
                }
                try
                {
                    // For these events, make sure to record s0 beforehand
                    replayEvent.randomS0Before = UnityEngine.Random.state.s0;
                    replayEvent.Replay(this);
                    // Only send the event if it played successfully and
                    // the IO says we shouldn't skip recording
                    if (!EventIO.SkipRecording)
                    {
                        EnqueueEventForSending(replayEvent);
                    }
                } catch (Exception e)
                {
                    Plugin.LogError($"Failed to replay event: {e}");
                    Plugin.LogError(e.ToString());
                }
            }
            IsReplayingEvents = false;

            // If we've replayed everythign for this tick and nothing's
            // triggered a desync, clear the saved stacks.
            //DeterminismController.PrintRandomStacks();
            DeterminismController.ClearRandomStacks();
        }

        /**
         * Readies and event for sending to connected players.
         * Adds the randomS0 if the event will be played, but assumed
         * this is called *before* the event is played. If this is 
         * called after an event is played, the randomS0 should already
         * be set (it will not be overwritten).
         */
        private static void EnqueueEventForSending(ReplayEvent replayEvent)
        {
            // Only set the random state if this recoded event is
            // actually going to be played, or if it's a heartbeat.
            // But don't overwrite the random state if it's already set.
            if (!replayEvent.randomS0Before.HasValue &&
                (EventIO.ShouldPlayPatchedEvents || replayEvent is HeartbeatEvent))
            {
                replayEvent.randomS0Before = UnityEngine.Random.state.s0;
                //Plugin.Log($"Recording event s0: {replayEvent.randomS0Before}");
            }
            eventsToSend.Enqueue(replayEvent);
        }

        private void SendEvents()
        {
            List<ReplayEvent> events = new List<ReplayEvent>();
            while (eventsToSend.TryDequeue(out ReplayEvent replayEvent))
            {
                replayEvent.ticksSinceLoad = ticksSinceLoad;
                events.Add(replayEvent);
            }
            // Don't send an empty list to save bandwidth.
            if (events.Count == 0) return;
            GroupedEvent group = new GroupedEvent(events);
            group.ticksSinceLoad = ticksSinceLoad;
            EventIO.Get().WriteEvents(group);
        }

        /**
         * Replays any pending events from the user or connected users
         * and then sends successful/pending events to connected users.
         * This should only be called if the game is paused at the end of
         * a tick or right at the start of a tick, so that events always
         * are recorded and replayed at the exact same time in the update loop.
         */
        private void DoTickIO()
        {
            ReplayEvents();
            SendEvents();
        }

        // TODO: Find a better callback way of waiting until initial game
        // loading and randomization is done.
        private int waitUpdates = 2;

        public void UpdateSingleton()
        {
            if (waitUpdates > 0)
            {
                waitUpdates--;
                return;
            }
            if (waitUpdates == 0)
            {
                IsLoaded = true;
                // Determinism just for testing
                UnityEngine.Random.InitState(1234);
                Plugin.Log("Setting random state to 1234");
                waitUpdates = -1;
            }
            if (io == null) return;
            io.Update();
            // Only replay events on Update if we're paused by the user.
            // Also only send events if paused, so the client doesn't play
            // then before the end of the tick.
            if (_speedManager.CurrentSpeed == 0 && TargetSpeed == 0)
            {
                DoTickIO();
            }
            UpdateSpeed();
        }

        public void SetTargetSpeed(int speed)
        {
            TargetSpeed = speed;
            // If we're paused, we should interrupt the ticking, so we end
            // before more of the tick happens.
            ShouldInterruptTicking = speed == 0;
            UpdateSpeed();
        }

        private void UpdateSpeed()
        {
            // Use for error repro
            //if (ticksSinceLoad > 320)
            //{
            //    SpeedChangePatcher.SetSpeedSilently(_speedManager, 0);
            //    return;
            //}
            if (io.IsOutOfEvents && _speedManager.CurrentSpeed != 0)
            {
                SpeedChangePatcher.SetSpeedSilently(_speedManager, 0);
            }
            if (io.IsOutOfEvents) return;
            int targetSpeed = this.TargetSpeed;
            int ticksBehind = io.TicksBehind;

            //Plugin.Log($"Ticks behind {ticksBehind}");
            // If we're behind, speed up to match.
            if (ticksBehind > targetSpeed)
            {
                targetSpeed = Math.Min(ticksBehind, 10);
                //Plugin.Log($"Upping target speed to: {targetSpeed}");
            }

            if (_speedManager.CurrentSpeed != targetSpeed)
            {
                //Plugin.Log($"Setting speed to target speed: {targetSpeed}");
                SpeedChangePatcher.SetSpeedSilently(_speedManager, targetSpeed);
            }
        }

        // This will always happen at the very begining of a tick before
        // anything else has happened, and after everything from the prior
        // tick (including parallel things) has finished.
        public void Tick()
        {
            if (io == null) return;

            if (io.ShouldSendHeartbeat)
            {
                // Add a heartbeat if needed to make sure all ticks have
                // at least 1 event, so the clients know we're ticking.
                EnqueueEventForSending(new HeartbeatEvent());
            }
            // Replay and send events at the change of a tick always.
            // For the server, sending events allows clients to keep playing.
            DoTickIO();

            // Log from IO
            io.Update();
            Plugin.Log($"Tick order hash: {TEBPatcher.EntityUpdateHash.ToString("X8")}; " +
                $"Move hash: {TEBPatcher.PositionHash.ToString("X8")}; " +
                $"Random s0: {UnityEngine.Random.state.s0.ToString("X8")}");

            if (ticksSinceLoad % 20 == 0)
            {
                //gameSaveHelper.LogStateCheck(ticksSinceLoad);
            }

            ticksSinceLoad++;

            // Update speed and pause if needed for the new tick.
            UpdateSpeed();
        }
    }

    [HarmonyPatch(typeof(TickableBucketService), nameof(TickableBucketService.FinishFullTick))]
    static class TickableBucketService_FinishFullTick_Patch
    {
        static void Postfix(TickableBucketService __instance)
        {
            Plugin.Log("Finishing full tick");
            Plugin.LogStackTrace();
        }
    }

    [HarmonyPatch(typeof(TickableSingletonService), nameof(TickableSingletonService.Load))]
    static class TickableSingletonServicePatcher
    {
        static void Postfix(TickableSingletonService __instance)
        {
            // Ensure late singletons come first
            // Create a new list, since the variable is immutable
            var tickableSingletons = new List<MeteredSingleton>(__instance._tickableSingletons);
            var earlySingletons = tickableSingletons
                .Where(s => s._tickableSingleton is IEarlyTickableSingleton).ToList();
            foreach ( var earlySingleton in earlySingletons)
            {
                tickableSingletons.Remove(earlySingleton);
            }
            tickableSingletons.InsertRange(0, earlySingletons);
            __instance._tickableSingletons = tickableSingletons.ToImmutableArray();

            //Plugin.Log("Loading tickables");
            //foreach (var singleton in __instance._tickableSingletons)
            //{
            //    var realSingleton = singleton._tickableSingleton;
            //    Plugin.Log($"Singleton: {realSingleton.GetType().Name}");
            //}
            //Plugin.Log("Loading parallel tickables");
            //foreach (var singleton in __instance._parallelTickableSingletons)
            //{
            //    Plugin.Log($"Parallel: {singleton.GetType().Name}");
            //}
        }
    }

    [HarmonyPatch(typeof(TickableBucketService), nameof(TickableBucketService.TickNextBucket))]
    static class TickableBucketServiceTick1Patcher
    {
        public static int currentBucket;

        static void Prefix(TickableBucketService __instance)
        {
            currentBucket = __instance._nextTickedBucketIndex;
        }
    }

    [HarmonyPatch(typeof(TickableBucketService), nameof(TickableBucketService.TickBuckets))]
    static class TickableBucketServiceTickUpdatePatcher
    {
        private static bool ShouldTick(TickableBucketService __instance, int numberOfBucketsToTick)
        {
            // Never tick if we've been interrupted by a forced pause
            if (ReplayService.ShouldInterruptTicking) return false;

            // If we've created an instance, make sure to go exactly until
            // the end of this tick, to ensure an update follows immediately.
            if (EntityComponentInstantiatePatcher.InstanceWasInstantiated)
            {
                Plugin.Log($"Finishing tick: {__instance._nextTickedBucketIndex}");
                return __instance._nextTickedBucketIndex != 0;
            }

            return numberOfBucketsToTick > 0;
        }

        static bool Prefix(TickableBucketService __instance, int numberOfBucketsToTick)
        {
            // TODO: I think if number of buckets starts at 0, we mark
            // instance instantiated and return

            if (numberOfBucketsToTick != 0)
            {
                numberOfBucketsToTick = __instance.NumberOfBuckets + 1;
            }

            while (ShouldTick(__instance, numberOfBucketsToTick--))
            {
                __instance.TickNextBucket();
            }

            if (EntityComponentInstantiatePatcher.InstanceWasInstantiated)
            {
                Plugin.Log($"Finishing full tick; {__instance._nextTickedBucketIndex}");
            }

            // Clear the flag after each round of updates
            EntityComponentInstantiatePatcher.InstanceWasInstantiated = false;

            // Replace the default behavior entirely
            return false;
        }
    }

    [HarmonyPatch(typeof(EntityService), nameof(EntityService.Instantiate), typeof(BaseComponent), typeof(Guid))]
    static class EntityComponentInstantiatePatcher
    {
        public static bool InstanceWasInstantiated = false;

        static void Postfix()
        {
            InstanceWasInstantiated = true;
        }
    }
}
