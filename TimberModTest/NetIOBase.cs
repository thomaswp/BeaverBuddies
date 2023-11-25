using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimberNet;
using UnityEngine;

namespace TimberModTest
{
    public abstract class NetIOBase<T> : EventIO where T : TimberNetBase
    {

        protected T netBase;
        public abstract bool PlayUserEvents { get; }
        public abstract bool RecordReplayedEvents { get; }
        public bool IsOutOfEvents => !netBase.ShouldTick;

        public void Close()
        {
            netBase.Close();
        }

        public void Update()
        {
            netBase.Update();
        }

        private ReplayEvent ToEvent(JObject obj)
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
            return netBase.ReadEvents(ticksSinceLoad)
                .Select(ToEvent)
                .Where(e => e != null)
                .ToList();
        }

        public void WriteEvents(params ReplayEvent[] events)
        {
            foreach (ReplayEvent e in events)
            {
                // TODO: It is silly to convert to JObject here, but not sure if there's
                // a better way to do it.
                netBase.DoUserInitiatedEvent(JObject.Parse(JsonSettings.Serialize(e)));
            }
        }
    }
}
