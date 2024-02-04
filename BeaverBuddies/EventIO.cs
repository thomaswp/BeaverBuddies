using System.Collections.Generic;
using BeaverBuddies.Events;

namespace BeaverBuddies
{
    public enum UserEventBehavior
    {
        Play,
        Send,
        QueuePlay,
    }

    public interface EventIO
    {
        public static ReplayConfig Config { get; set; }

        void Update();

        List<ReplayEvent> ReadEvents(int ticksSinceLoad);

        void WriteEvents(params ReplayEvent[] events);

        void Close();

        /**
         * Return true if the game should record a received event
         * being replayed.
         */
        bool RecordReplayedEvents { get; }

        /**
         * Returns what the replay service should do with events
         * recoded that are user-initiated.
         */
        UserEventBehavior UserEventBehavior { get; }

        bool IsOutOfEvents { get; }
        int TicksBehind { get; }

        /**
         * Should return true if this IO should send hearbeats on tick
         */
        bool ShouldSendHeartbeat {  get; }

        private static EventIO instance;

        public static bool IsNull => instance == null;

        public static EventIO Get() { return instance; }
        public static void Set(EventIO io) 
        {
            if (io != instance)
            {
                // Clean up the old IO (if it exists)
                Reset();
            }
            instance = io; 
        }

        public static void Reset()
        {
            if (instance != null)
            {
                Plugin.Log("Closing EventIO...");
                instance.Close();
                instance = null;
                Plugin.Log("Success!");
            }
        }

        public static bool ShouldPauseTicking
        {
            get
            {
                EventIO io = Get();
                if (io == null) return false;
                return io.IsOutOfEvents;
            }
        }

        /**
         * Returns true if the game should carry out user-initiated
         * events.
         */
        public static bool ShouldPlayPatchedEvents
        {
            get
            {
                // If the events are being replayed, we should
                // always play them (i.e. they're not user-initiated).
                if (ReplayService.IsReplayingEvents)
                {
                    return true;
                }
                EventIO io = Get();
                if (io == null) return true;
                return io.UserEventBehavior == UserEventBehavior.Play;
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
