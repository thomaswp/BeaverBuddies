﻿// Define to force game to run a full tick each
// update, rather than amortizing ticks over multiple.
//#define ONE_TICK_PER_UPDATE

using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.CoreUI;
using Timberborn.DemolishingUI;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.Goods;
using Timberborn.Options;
using Timberborn.PlantingUI;
using Timberborn.ScienceSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WorkSystem;
using Timberborn.WorkSystemUI;
using BeaverBuddies.Events;
using static Timberborn.TickSystem.TickableSingletonService;
using static BeaverBuddies.SingletonManager;
using BeaverBuddies.Connect;
using static UnityEngine.ParticleSystem.PlaybackState;
using BeaverBuddies.DesyncDetecter;
using Timberborn.WebNavigation;
using System.Collections.Immutable;
using BeaverBuddies.IO;

namespace BeaverBuddies
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

    public class ReplayService : RegisteredSingleton, IReplayContext, IPostLoadableSingleton, IUpdatableSingleton, IResettableSingleton
    {
        //private readonly TickWathcerService _tickWathcerService;
        private readonly EventBus _eventBus;
        private readonly SpeedManager _speedManager;
        private readonly GameSaver _gameSaver;
        private readonly ISingletonRepository _singletonRepository;
        private readonly TickingService _tickingService;
        private readonly DeterminismService _determinismService;

        private readonly GameSaveHelper gameSaveHelper;

        private List<object> singletons = new();

        // TODO: I believe that this could be a non-static variable
        // set at initialization time, which would prevent accidentally
        // accessing a new game's event IO.
        private EventIO io => EventIO.Get();

        private int __ticksSinceLoad = 0;
        private int ticksSinceLoad 
        { 
            get => __ticksSinceLoad;
            set
            {
                __ticksSinceLoad = value;
                TimeTimePatcher.SetTicksSinceLoaded(value);
                DesyncDetecterService.StartTick(value);
            }
        }
        public int TicksSinceLoad => ticksSinceLoad;

        public float TargetSpeed  { get; private set; } = 0;
        public bool IsDesynced { get; private set; } = false;

        private ConcurrentQueue<ReplayEvent> eventsToSend = new ConcurrentQueue<ReplayEvent>();
        private ConcurrentQueue<ReplayEvent> eventsToPlay = new ConcurrentQueue<ReplayEvent>();

        /// <summary>
        /// Returns true if the ReplayService is ready for the game to
        /// start a new a tick, and false if not (e.g. because the client
        /// has not yet received a heartbeat from the server for the next tick).
        /// </summary>
        public bool IsReadyToStartTick {
            get
            {
                // The client shouldn't tick until the server has sent a heartbeat
                // Check the *next* tick, since current tick has already happened
                return !(io is ClientEventIO && !io.HasEventsForTick(TicksSinceLoad + 1));
            }
        }

        public static bool IsLoaded { get; private set; } = false;
        private bool isReset = false;

        private bool CanAct => io != null && !isReset && !IsDesynced;

        public static bool IsReplayingEvents { get; private set; } = false;

        public void Reset()
        {
            Plugin.Log("Resetting Replay Service...");
            IsLoaded = false;
            IsReplayingEvents = false;
            isReset = true;
        }

        public ReplayService(
            EventBus eventBus,
            SpeedManager speedManager,
            GameSaver gameSaver,
            ISingletonRepository singletonRepository,
            TickingService tickingService,
            DeterminismService determinismService,
            BlockObjectPlacerService blockObjectPlacerService,
            BuildingService buildingService,
            PlantingSelectionService plantingSelectionService,
            TreeCuttingArea treeCuttingArea,
            EntityRegistry entityRegistry,
            EntityService entityService,
            RecipeSpecificationService recipeSpecificationService,
            DemolishableSelectionTool demolishableSelectionTool,
            DemolishableUnselectionTool demolishableUnselectionTool,
            BuildingUnlockingService buildingUnlockingService,
            WorkingHoursManager workingHoursManager,
            WorkingHoursPanel workingHoursPanel,
            WorkplaceUnlockingService workplaceUnlockingService,
            IOptionsBox optionsBox,
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            RehostingService rehostingService
        )
        {
            //_tickWathcerService = AddSingleton(tickWathcerService);
            _eventBus = AddSingleton(eventBus);
            _speedManager = AddSingleton(speedManager);
            _gameSaver = AddSingleton(gameSaver);
            _singletonRepository = AddSingleton(singletonRepository);
            _tickingService = AddSingleton(tickingService);
            _determinismService = AddSingleton(determinismService);
            AddSingleton(blockObjectPlacerService);
            AddSingleton(buildingService);
            AddSingleton(plantingSelectionService);
            AddSingleton(treeCuttingArea);
            AddSingleton(entityRegistry);
            AddSingleton(entityService);
            AddSingleton(recipeSpecificationService);
            AddSingleton(demolishableSelectionTool);
            AddSingleton(demolishableUnselectionTool);
            AddSingleton(buildingUnlockingService);
            AddSingleton(workingHoursManager);
            AddSingleton(workingHoursPanel);
            AddSingleton(workplaceUnlockingService);
            AddSingleton(optionsBox);
            AddSingleton(dialogBoxShower);
            AddSingleton(urlOpener);
            AddSingleton(rehostingService);

            AddSingleton(this);

            _eventBus.Register(this);

            gameSaveHelper = new GameSaveHelper(gameSaver);

            //io = new FileWriteIO("test.json");
            //io = new FileReadIO("planting.json");
            //io = new FileReadIO("trees.json");

            _tickingService.replayService = this;
        }

        public void SetTicksSinceLoad(int ticks)
        {
            Plugin.Log($"Setting ticks since load to: {ticks}");
            ticksSinceLoad = ticks;
        }

        public void PostLoad()
        {
            Plugin.Log("PostLoad");
            _determinismService.UnityThread = Thread.CurrentThread;
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
            // TODO: This doesn't seem to work
            return _singletonRepository.GetSingletons<T>().FirstOrDefault();
        }

        public void RecordEvent(ReplayEvent replayEvent)
        {
            // During a replay, we save things manually, only if they're
            // successful.
            if (IsReplayingEvents) return;
            if (!IsLoaded) return;

            string json = JsonSettings.Serialize(replayEvent);
            Plugin.Log($"RecordEvent: {json}");

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

        private List<ReplayEvent> ReadEventsFromIO(int tick)
        {
            List<ReplayEvent> eventsToReplay = io.ReadEvents(tick);
            // Spread grouped events into a flat list because we need to
            // replay each individually (since we only record them if successful).
            eventsToReplay = eventsToReplay
                .SelectMany(e => e is GroupedEvent grouped ? grouped.events.ToArray() : new ReplayEvent[] { e })
                .ToList();
            return eventsToReplay;
        }

        private void ReplayEvents()
        {
            if (_tickingService.NextBucket != 0)
            {
                Plugin.LogWarning($"Warning, replaying events when bucket != 0: {_tickingService.NextBucket}");
            }

            // Start with any events received from connected users
            List<ReplayEvent> eventsToReplay = ReadEventsFromIO(TicksSinceLoad);
            // Then add any from this user that have been deferred
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
                // Skip if we're in Debug mode, since we'll get more details
                // if we look at the full trace.
                if (!EventIO.Config.Debug && replayEvent.randomS0Before != null)
                {
                    int s0 = UnityEngine.Random.state.s0;
                    int randomS0Before = (int)replayEvent.randomS0Before;
                    if (s0 != randomS0Before)
                    {
                        Plugin.LogWarning($"Random state mismatch: {s0:X8} != {randomS0Before:X8}");
                        HandleDesync();
                        break;
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
        }

        public void HandleDesync()
        {
            if (IsDesynced) return;

            ClientDesyncedEvent e = new ClientDesyncedEvent();
            // Set IsDesynced to true so event play instead of sending
            // to the host, allowing the Client to continue play.
            IsDesynced = true;
            // Don't use EnqueueEventForSending because it shouldn't
            // have a random state set.
            eventsToSend.Enqueue(e);
            e.Replay(this);
            // Send events immediately to get this event out before resetting
            // the EventIO
            // TODO: This only works because sending events is currently a synchronous
            // operation, and it really shouldn't be, so this is a short-term fix!
            SendEvents();
            // Pause
            SpeedChangePatcher.SetSpeedSilently(_speedManager, 0);
            EventIO.Reset();
        }

        /**
         * Readies and event for sending to connected players.
         * Adds the randomS0 if the event will be played, but assumed
         * this is called *before* the event is played. If this is 
         * called after an event is played, the randomS0 should already
         * be set (it will not be overwritten).
         */
        private void EnqueueEventForSending(ReplayEvent replayEvent)
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
            if (EventIO.IsNull) return;
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

        private void Initialize()
        {
            // Start tick at 0
            DesyncDetecterService.StartTick(ticksSinceLoad);

            IsLoaded = true;

        }

        // TODO: Find a better callback way of waiting until initial game
        // loading and randomization is done.
        private int waitUpdates = 2;

        public void UpdateSingleton()
        {
            if (!CanAct) return;
            if (waitUpdates > 0)
            {
                waitUpdates--;
                return;
            }
            if (waitUpdates == 0)
            {
                Initialize();
                waitUpdates = -1;
            }
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

        public void SetTargetSpeed(float speed)
        {
            TargetSpeed = speed;
            UpdateSpeed();
        }

        private void UpdateSpeed()
        {
            if (EventIO.IsNull) return;

            if (io.IsOutOfEvents)
            {
                // Also pause the game (silently) if we're out of events
                if (_speedManager.CurrentSpeed != 0)
                {
                    SpeedChangePatcher.SetSpeedSilently(_speedManager, 0);
                }
                // And return early
                return;
            }

            // If we're not out of ticks to process, check if we're behind
            float targetSpeed = TargetSpeed;
            int ticksBehind = io.TicksBehind;

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

        // This will be called at the very begining of a tick before
        // anything else has happened, and after everything from the prior
        // tick (including parallel things) has finished.
        public void DoTick()
        {
            if (!CanAct) return;

            if (EventIO.Config.Debug && io.ShouldSendHeartbeat)
            {
                // Before incrementing the tick (which creates a new blank trace),
                // capture any unsent traces and send them.
                // Note: this will capture traces for prior ticks, but be sent with
                // an event at the start of the *upcoming* tick.
                foreach (var e in DesyncDetecterService.CreateReplayEventsAndClear())
                {
                    EnqueueEventForSending(e);
                }
            }

            ticksSinceLoad++;

            if (io.ShouldSendHeartbeat)
            {
                // Add a heartbeat if needed to make sure all ticks have
                // at least 1 event, so the clients know we're ticking.
                EnqueueEventForSending(new HeartbeatEvent());
            }
            // Replay and send events at the change of a tick always.
            // For the server, sending events allows clients to keep playing.
            DoTickIO();

            // Remember DoTickIO can set EventIO to null

            // Log from IO
            io?.Update();

            // IO Complete for Tick
            Plugin.Log($"Tick {ticksSinceLoad:D5} IO done; " +
                $"Order hash: {TEBPatcher.EntityUpdateHash:X8}; " +
                $"Move hash: {TEBPatcher.PositionHash:X8}; " +
                $"Random s0: {UnityEngine.Random.state.s0:X8}");

            if (ticksSinceLoad % 20 == 0)
            {
                //gameSaveHelper.LogStateCheck(ticksSinceLoad);
            }

            // Update speed and pause if needed for the new tick.
            UpdateSpeed();

            if (io is ServerEventIO && ticksSinceLoad == 1)
            {
                ((ServerEventIO)io).StopAcceptingClients();
            }
        }

        public void FinishFullTickIfNeededAndThen(Action action)
        {
            // If we're paused, we should be at the end of a tick anyway
            if (_speedManager.CurrentSpeed == 0)
            {
                action();
                return;
            }
            // If we're not paused, we need to wait until the end of the tick
            _tickingService.FinishFullTickAndThen(action);
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
        }
    }

    public class TickingService : RegisteredSingleton
    {
        /// <summary>
        /// If true, the TickingService will stop as soon as possible (interrupting
        /// a normal update, but finishing it's current bucket), but it will then resume
        /// on the following update.
        /// </summary>
        public bool ShouldInterruptTicking { get; set; } = false;
        public bool ShouldCompleteFullTick { get; private set; } = false;

        public bool HasTickedReplayService { get; private set; } = false;

        public int NextBucket { get; private set; } = 0;

        public ReplayService replayService { get; set; }

        // Should be ok non-concurrent - for now only main thread call this
        private List<Action> onCompletedFullTick = new List<Action>();

        public void FinishFullTick()
        {
            ShouldCompleteFullTick = true;
        }

        public void FinishFullTickAndThen(Action value)
        {
            onCompletedFullTick.Add(value);
            ShouldCompleteFullTick = true;
        }

        internal void OnTickingCompleted()
        {
            // Interruptions are always temporary and get reset at the end of
            // each ticking update
            ShouldInterruptTicking = false;
            if (!ShouldCompleteFullTick) return;
            Plugin.Log($"Finished full tick; calling {onCompletedFullTick.Count} callbacks");
            foreach (var action in onCompletedFullTick)
            {
                action();
            }
            onCompletedFullTick.Clear();
            ShouldCompleteFullTick = false;
        }

        private bool ShouldTick(TickableBucketService __instance, int numberOfBucketsToTick)
        {

            // Don't tick if we've been interrupted by a forced pause
            if (ShouldInterruptTicking) return false;

            // Don't tick if we've set the game speed to 0 (paused)
            if (replayService.TargetSpeed == 0) return false;

            if (IsAtStartOfTick(__instance) && !HasTickedReplayService)
            {
                // Don't start a brand new tick until the ReplayService
                // is ready. Note that this is only a reason to stop if
                // we're at the *very start* of a new tick - otherwise
                // it would be overly conservative.
                if (!replayService.IsReadyToStartTick)
                {
                    // In theory the game should be paused to prevent this, but some logs
                    // suggest the client can get ahead of the server, which would
                    // trigger this warning (and now prevent the client's tick)
                    if (replayService.TicksSinceLoad > 0)
                    {
                        Plugin.LogWarning($"Client trying to tick before receiving " +
                            $"Heartbeat at tick: {replayService.TicksSinceLoad}");
                    }
                    return false;
                }
            }

            // If we need to complete a full tick, make sure to go exactly until
            // the end of this tick, to ensure an update follows immediately.
            if (ShouldCompleteFullTick)
            {
                return __instance._nextTickedBucketIndex != 0;
            }
            return numberOfBucketsToTick > 0;
        }

        public static bool IsAtStartOfTick(TickableBucketService __instance)
        {
            return __instance._nextTickedBucketIndex == 0 &&
                !__instance._tickedSingletons;
        }

        private bool TickReplayServiceOrNextBucket(TickableBucketService __instance)
        {
            if (IsAtStartOfTick(__instance))
            {
                // If we're at the start of a tick, and we haven't yet
                // ticked the ReplayService...
                if (!HasTickedReplayService)
                {
                    // First finish any parallel ticks
                    ((TickableSingletonService)__instance._tickableSingletonService).FinishParallelTick();

                    // Tick it and stop
                    HasTickedReplayService = true;
                    replayService?.DoTick();
                    return true;
                }
                // Otherwise if we're still at the beginning
                // reset the flag
                HasTickedReplayService = false;
            }
            __instance.TickNextBucket();
            NextBucket = __instance._nextTickedBucketIndex;
            return false;
        }

        public bool TickBuckets(TickableBucketService __instance, int numberOfBucketsToTick)
        {

            // TODO: I think if number of buckets starts at 0, we should unmark
            // complete full tick and return because it means we're paused...
            // Alternatively, I think that we could use the ReplayService version
            // that only stops ticking if not paused

#if ONE_TICK_PER_UPDATE
            // Forces 1 tick per update
            if (numberOfBucketsToTick != 0)
            {
                numberOfBucketsToTick = __instance.NumberOfBuckets + 1;
            }
#endif

            while (ShouldTick(__instance, numberOfBucketsToTick--))
            {
                if (TickReplayServiceOrNextBucket(__instance))
                {
                    // Refund a bucket if we ticked the ReplayService
                    numberOfBucketsToTick++;
                }
            }

            // Tell the TickRequester we've finished this partial (or possibly complete) tick
            OnTickingCompleted();

            // Replace the default behavior entirely
            return false;
        }
    }

    [ManualMethodOverwrite]
    /*
        7/20/2024
        // Also check TickNextBucket - we rework everything
		while (numberOfBucketsToTick-- > 0)
		{
			TickNextBucket();
		}
     */
    [HarmonyPatch(typeof(TickableBucketService), nameof(TickableBucketService.TickBuckets))]
    static class TickableBucketServiceTickUpdatePatcher
    {
        static bool Prefix(TickableBucketService __instance, int numberOfBucketsToTick)
        {
            if (EventIO.IsNull) return true;
            TickingService ts = GetSingleton<TickingService>();
            if (ts == null) return true;
            return ts.TickBuckets(__instance, numberOfBucketsToTick);
        }
    }
}
