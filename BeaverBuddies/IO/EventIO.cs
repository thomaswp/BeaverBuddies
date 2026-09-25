using System;
using System.Collections.Generic;
using BeaverBuddies.Events;
using Newtonsoft.Json.Linq;

namespace BeaverBuddies.IO
{
    public enum UserEventBehavior
    {
        Play,
        Send,
        QueuePlay,
    }

    public interface EventIO
    {
        void Update();

        List<ReplayEvent> ReadEvents(int ticksSinceLoad);

        void WriteEvents(params ReplayEvent[] events);

        /**
         * Sends a message to the other players right away, outside of
         * the tick queue. It isn't recorded or replayed, so it must not
         * affect the game state.
         */
        void SendTransientMessage(JObject message);

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
        bool ShouldSendHeartbeat { get; }

        bool HasEventsForTick(int tick);

        private static EventIO instance;

        /**
         * Raised on the main thread when another player sends a
         * transient message.
         */
        public static event Action<JObject> TransientMessageReceived;

        public static void RaiseTransientMessageReceived(JObject message)
        {
            TransientMessageReceived?.Invoke(message);
        }

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
