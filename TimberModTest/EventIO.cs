using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TimberModTest
{
    public interface EventIO
    {
        // TODO: Remove
        public static bool IsClient = false;

        void Update();

        List<ReplayEvent> ReadEvents(int ticksSinceLoad);

        void WriteEvents(params ReplayEvent[] events);

        void Close();

        /**
         * Return true if the game should carry out a user-initiated
         * event that is being recorded.
         */
        bool PlayRecordedEvents { get; }
        
        /**
         * Return true if the game should record a received event
         * being replayed.
         */
        bool RecordReplayedEvents { get; }
        bool IsOutOfEvents { get; }

        private static EventIO instance;
        public static EventIO Get() { return instance; }
        public static void Set(EventIO io) { instance = io; }

        public static bool ShouldPlayRecordedEvents
        {
            get
            {
                EventIO io = Get();
                if (io == null) return true;
                return io.PlayRecordedEvents;
            }
        }

        /**
         * Returns true if the game should play events without
         * recording them right now (e.g., if we are currently
         * replaying events).
         */
        public static bool SkipRecording
        {
            get
            {
                if (!ReplayService.IsReplayingEvents) return false;
                EventIO io = Get();
                if (io == null) return false;
                return !io.RecordReplayedEvents;
            }
        }
    }
}
