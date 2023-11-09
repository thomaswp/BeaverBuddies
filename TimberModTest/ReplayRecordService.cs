using System;
using System.Collections.Generic;
using System.Text;

namespace TimberModTest
{
    public class ReplayService : IReplayContext
    {
        private TickWathcerService _tickWathcerService;

        // TODO: Ideally there would be a way for this not to be static
        // but the events are generated from static patch events that won't
        // have consistent access to any service, so I need something to be static.
        private static ReplayService instance;


        private List<ReplayEvent> eventsToReplay = new List<ReplayEvent>();
        private EventIO io;

        public ReplayService(TickWathcerService tickWathcerService)
        {
            _tickWathcerService = tickWathcerService;
            instance = this;
            io = new FileWriteIO("test.json");
        }

        public static void RecordEvent(ReplayEvent replayEvent)
        {
            instance.RecordEventInternal(replayEvent);
        }

        void RecordEventInternal(ReplayEvent replayEvent)
        {
            if (_tickWathcerService != null)
            {
                replayEvent.timeInFixedSecs =
                    _tickWathcerService.TotalTimeInFixedSecons;
            }
            io.WriteEvents(replayEvent);
        }

        static void UpdateInstance()
        {
            instance.Update();
        }

        private void ReplayEvents()
        {
            float currentTime = _tickWathcerService.TotalTimeInFixedSecons;
            for (int i = 0; i < eventsToReplay.Count; i++)
            {
                ReplayEvent replayEvent = eventsToReplay[i];
                float eventTime = replayEvent.timeInFixedSecs;
                if (eventTime > currentTime)
                    break;
                if (eventTime < currentTime)
                {
                    Plugin.Log($"Event past time: {eventTime} < {currentTime}");
                }
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
