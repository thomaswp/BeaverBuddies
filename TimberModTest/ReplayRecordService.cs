using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;

namespace TimberModTest
{
    public class ReplayService : IReplayContext, IPostLoadableSingleton
    {
        private readonly TickWathcerService _tickWathcerService;
        private readonly EventBus _eventBus;
        //public readonly IBlockObjectPlacer buildingPlacer;
        //public readonly BuildingService buildingService;

        // TODO: Ideally there would be a way for this not to be static
        // but the events are generated from static patch events that won't
        // have consistent access to any service, so I need something to be static.
        private static ReplayService instance;

        private List<object> singletons = new();

        private List<ReplayEvent> eventsToReplay = new List<ReplayEvent>();
        private EventIO io;

        public ReplayService(
            TickWathcerService tickWathcerService,
            EventBus eventBus,
            //IBlockObjectPlacer buildingPlacer,
            BlockObjectPlacerService blockObjectPlacerService,
            BuildingService buildingService,
            PlantingSelectionService plantingSelectionService
        )
        {
            _tickWathcerService = AddSingleton(tickWathcerService);
            _eventBus = AddSingleton(eventBus);
            AddSingleton(blockObjectPlacerService);
            AddSingleton(buildingService);
            AddSingleton(plantingSelectionService);
            instance = this;
            //io = new FileWriteIO("test.json");
            io = new FileReadIO("planting.json");
        }

        public void PostLoad()
        {
            // TODO: Make this random, but then send the seed as the first event.
            Plugin.Log("Setting random state and loading events");
            UnityEngine.Random.InitState(1234);
            Update();

            //Plugin.Log(new System.Diagnostics.StackTrace().ToString());
            //Plugin.Log("All Game events:");
            //foreach (var key in _eventBus._subscriptions._subscriptions.Keys)
            //{
            //    Plugin.Log($"EventBus subscription: {key.Name}");
            //}
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
            instance.RecordEventInternal(replayEvent);
        }

        void RecordEventInternal(ReplayEvent replayEvent)
        {
            replayEvent.SetTime(_tickWathcerService);
            io.WriteEvents(replayEvent);
        }

        public static void UpdateInstance()
        {
            instance.Update();
        }

        private void ReplayEvents()
        {
            int currentTick = _tickWathcerService.TicksSinceLoad;
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
                replayEvent.Replay(this);
                eventsToReplay.RemoveAt(i);
                i--;

                if (eventsToReplay.Count == 0)
                {
                    Plugin.Log("Events complete: should pause!");
                    // TODO: Pause
                }
            }
        }

        private void Update()
        {
            eventsToReplay.AddRange(io.ReadEvents());
            eventsToReplay.Sort();
            ReplayEvents();
        }

    }
}
