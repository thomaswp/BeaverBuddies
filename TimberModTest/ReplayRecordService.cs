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
        private int speedAtPause = 0;
        private bool changingSpeed = false;

        private static ConcurrentQueue<ReplayEvent> eventsToSend = new ConcurrentQueue<ReplayEvent>();

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
            eventsToSend.Enqueue(replayEvent);
        }

        private void ReplayEvents()
        {
            List<ReplayEvent> eventsToReplay = io.ReadEvents(ticksSinceLoad);

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
                try
                {
                    replayEvent.Replay(this);
                    // Only send the event if it played successfully and
                    // the IO says we shouldn't skip recording
                    if (!EventIO.SkipRecording)
                    { 
                        eventsToSend.Enqueue(replayEvent);
                    }
                } catch (Exception e)
                {
                    Plugin.Log($"Failed to replay event: {e}");
                    Plugin.Log(e.ToString());
                }
            }
            IsReplayingEvents = false;
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

            // TODO: Should have a more intelligent speed buffering algorith...
            changingSpeed = true;
            if (io.IsOutOfEvents && _speedManager.CurrentSpeed != 0)
            {
                speedAtPause = _speedManager.CurrentSpeed;
                _speedManager.ChangeSpeed(0);
            }
            else if (!io.IsOutOfEvents && speedAtPause != 0 && 
                _speedManager.CurrentSpeed == 0)
            {
                _speedManager.ChangeSpeed(speedAtPause);
                speedAtPause = 0;
            }
            changingSpeed = false;
        }

        public void OnSpeedChange(CurrentSpeedChangedEvent e)
        {
            if (changingSpeed) return;
            speedAtPause = e.CurrentSpeed;
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

    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.ChangeSpeed))]
    public class SpeedChangePatcher
    {

        //private static int times1 = 0;

        static void Prefix(SpeedManager __instance, ref int speed)
        {
            if (EventIO.Get().IsOutOfEvents) speed = 0;

            //if (speed != 0 && times1 == 0)
            //{
            //    Plugin.Log("Setting seed on play");
            //    //ReplayService.RecordEvent(RandomStateSetEvent.CreateAndExecute());
            //    UnityEngine.Random.InitState(1234);
            //    times1++;
            //}

            // TODO: Do all the good replay ifs as in other events
            // and make sure this isn't triggered programmatically
            //if (__instance.CurrentSpeed != speed)
            //{
            //    ReplayService.RecordEvent(new SpeedSetEvent()
            //    {
            //        speed = speed
            //    });
            //}
        }
    }


}
