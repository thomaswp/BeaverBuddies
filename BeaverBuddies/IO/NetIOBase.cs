using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using BeaverBuddies.Events;
using TimberNet;
using BeaverBuddies.Steam;

namespace BeaverBuddies.IO
{
    public abstract class NetIOBase<T> : EventIO where T : TimberNetBase
    {

        protected T netBase;
        public abstract bool RecordReplayedEvents { get; }
        public abstract bool ShouldSendHeartbeat { get; }
        public abstract UserEventBehavior UserEventBehavior { get; }
        public bool IsOutOfEvents => netBase == null ? true : !netBase.ShouldTick;
        public int TicksBehind => netBase == null ? 0 : netBase.TicksBehind;

        private SteamPacketListener steamPacketListener = null;

        public void Close()
        {
            if (netBase == null) return;
            netBase.Close();
        }

        public void Update()
        {
            if (netBase == null) return;
            netBase.Update();
            steamPacketListener?.Update();
        }

        private static ReplayEvent ToEvent(JObject obj)
        {
            //Plugin.Log($"Recieving {obj}");
            try
            {
                return JsonSettings.Deserialize<ReplayEvent>(obj.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
            return null;
        }

        public List<ReplayEvent> ReadEvents(int ticksSinceLoad)
        {
            if (netBase == null) return new List<ReplayEvent>();
            return netBase.ReadEvents(ticksSinceLoad)
                .Select(ToEvent)
                .Where(e => e != null)
                .ToList();
        }

        public virtual void WriteEvents(params ReplayEvent[] events)
        {
            if (netBase == null) return;
            foreach (ReplayEvent e in events)
            {
                // TODO: It is silly to convert to JObject here, but not sure if there's
                // a better way to do it.
                netBase.DoUserInitiatedEvent(JObject.Parse(JsonSettings.Serialize(e)));
            }
        }

        public bool HasEventsForTick(int tick)
        {
            if (netBase == null) return false;
            return netBase.HasEventsForTick(tick);
        }

        protected void TryRegisterSteamPacketReceiver(object receiver)
        {
            if (!(receiver is ISteamPacketReceiver)) return;
            if (steamPacketListener == null)
            {
                steamPacketListener = new SteamPacketListener();
            }
            ((ISteamPacketReceiver)receiver).RegisterSteamPacketListener(steamPacketListener);
        }
    }
}
