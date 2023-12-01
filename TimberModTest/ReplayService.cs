using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.Forestry;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using TimberNet;
using UnityEngine;

namespace TimberModTest
{
    public class ReplayService : IReplayContext, IPostLoadableSingleton, IUpdatableSingleton, ITickableSingleton
    {
        //private readonly TickWathcerService _tickWathcerService;
        private readonly EventBus _eventBus;
        private readonly SpeedManager _speedManager;
        private readonly GameSaver _gameSaver;

        private List<object> singletons = new();

        private EventIO io => EventIO.Get();

        private int ticksSinceLoad = 0;
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

            AddSingleton(this);

            _eventBus.Register(this);

            //io = new FileWriteIO("test.json");
            //io = new FileReadIO("planting.json");
            //io = new FileReadIO("trees.json");
        }

        public void PostLoad()
        {
            Plugin.Log("PostLoad");
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
                SendEvent(replayEvent);
            }
        }

        private void ReplayEvents()
        {
            List<ReplayEvent> eventsToReplay = io.ReadEvents(ticksSinceLoad);
            while (eventsToPlay.TryDequeue(out ReplayEvent replayEvent))
            {
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
                    Plugin.Log($"Event past time: {eventTime} < {currentTick}");
                }
                Plugin.Log($"Replaying event [{replayEvent.ticksSinceLoad}]: {replayEvent.type}");
                
                // If this event was played (e.g. on the server) and recorded a 
                // random state, make sure we're in the same state.
                if (replayEvent.randomS0Before != null)
                {
                    int s0 = UnityEngine.Random.state.s0;
                    if (s0 != replayEvent.randomS0Before)
                    {
                        Plugin.Log($"Random state mismatch: {s0} != {replayEvent.randomS0Before}");
                        // TODO: Resync!
                    }
                }
                try
                {
                    replayEvent.Replay(this);
                    // Only send the event if it played successfully and
                    // the IO says we shouldn't skip recording
                    if (!EventIO.SkipRecording)
                    {
                        SendEvent(replayEvent);
                    }
                } catch (Exception e)
                {
                    Plugin.Log($"Failed to replay event: {e}");
                    Plugin.Log(e.ToString());
                }
            }
            IsReplayingEvents = false;
        }

        private static void SendEvent(ReplayEvent replayEvent)
        {
            // Only set the random state if this recoded event is
            // actually going to be played.
            if (EventIO.ShouldPlayPatchedEvents)
            {
                replayEvent.randomS0Before = UnityEngine.Random.state.s0;
                Plugin.Log($"Recording event s0: {replayEvent.randomS0Before}");
            }
            eventsToSend.Enqueue(replayEvent);
        }

        private void SendEvents()
        {
            while (eventsToSend.TryDequeue(out ReplayEvent replayEvent))
            {
                replayEvent.ticksSinceLoad = ticksSinceLoad;
                EventIO.Get().WriteEvents(replayEvent);
            }
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
            ReplayEvents();
            SendEvents();
            UpdateSpeed();
        }

        public void SetTargetSpeed(int speed)
        {
            TargetSpeed = speed;
            UpdateSpeed();
        }

        private void UpdateSpeed()
        {
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
                targetSpeed = Math.Min(ticksBehind, 4);
                //Plugin.Log($"Upping target speed to: {targetSpeed}");
            }

            if (_speedManager.CurrentSpeed != targetSpeed)
            {
                //Plugin.Log($"Setting speed to target speed: {targetSpeed}");
                SpeedChangePatcher.SetSpeedSilently(_speedManager, targetSpeed);
            }
        }

        //private Stopwatch tickTimer = new Stopwatch();
        public void Tick()
        {
            // TODO: Should probably save and send the random seed each time
            // and if it doesn't match, resync

            //Plugin.Log($"Tick in {tickTimer.ElapsedMilliseconds}ms");
            UpdateSingleton();
            ticksSinceLoad++;
            //tickTimer.Restart();

            if (ticksSinceLoad % 100 == 0)
            {
                LogStateCheck();
            }
        }

        private void LogStateCheck()
        {
            // TODO: This doesn't work yet.
            // There is definitely a timestamp in the save, which is part of
            // the issue. Need to test by saving to a file and unzipping/comparing.
            // There may also be tiny amounts of nondeterminism somewhere
            // (possibly harmless, and possibly problematic).
            // And it may be there are small rounding errors on things like seconds.
            //MemoryStream ms = new MemoryStream();
            //_gameSaver.Save(ms);
            //byte[] bytes = ms.ToArray();
            //int hash = TimberNetBase.GetHashCode(bytes);
            //Plugin.Log($"State Check [T{ticksSinceLoad}]: {hash.ToString("X8")}");
        }
    }
}
