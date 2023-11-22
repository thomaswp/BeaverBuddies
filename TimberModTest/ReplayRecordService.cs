using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.Forestry;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;

namespace TimberModTest
{
    public class ReplayService : IReplayContext, IPostLoadableSingleton, IUpdatableSingleton, ITickableSingleton
    {
        //private readonly TickWathcerService _tickWathcerService;
        private readonly EventBus _eventBus;

        private List<object> singletons = new();

        private EventIO io => EventIO.Get();

        private int ticksSinceLoad = 0;

        private static ConcurrentQueue<ReplayEvent> eventsToSend = new ConcurrentQueue<ReplayEvent>();

        public static bool IsReplayingEvents { get; private set; } = false;

        public ReplayService(
            //TickWathcerService tickWathcerService,
            EventBus eventBus,
            //IBlockObjectPlacer buildingPlacer,
            BlockObjectPlacerService blockObjectPlacerService,
            BuildingService buildingService,
            PlantingSelectionService plantingSelectionService,
            TreeCuttingArea treeCuttingArea
        )
        {
            //_tickWathcerService = AddSingleton(tickWathcerService);
            _eventBus = AddSingleton(eventBus);
            AddSingleton(blockObjectPlacerService);
            AddSingleton(buildingService);
            AddSingleton(plantingSelectionService);
            AddSingleton(treeCuttingArea);

            //io = new FileWriteIO("test.json");
            //io = new FileReadIO("planting.json");
            //io = new FileReadIO("trees.json");
        }

        public void PostLoad()
        {
            // TODO: Make this random, but then send the seed as the first event.
            Plugin.Log("Setting random state and loading events");
            UnityEngine.Random.InitState(1234);
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
            eventsToSend.Enqueue(replayEvent);
        }

        private void ReplayEvents()
        {
            List<ReplayEvent> eventsToReplay = io.ReadEvents(ticksSinceLoad);

            // TODO: Need a way when replaying to avoid
            // recording any of these events again (since the
            // server does so on its own automatically)
            // (Though alternatively... it may be better to do it
            // this way so things stay more synced, and since some
            // events may need to be canceled if they fail...)
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

        public void UpdateSingleton()
        {
            io.Update();
            ReplayEvents();
            SendEvents();
        }

        public void Tick()
        {
            UpdateSingleton();
            ticksSinceLoad++;
        }
    }
}
