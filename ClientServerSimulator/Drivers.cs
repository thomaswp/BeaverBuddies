using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.AxHost;

namespace TimberNet
{
    internal abstract class DriverBase<T> where T : TimberNetBase
    {
        public const string ADDRESS = TimberNetBase.HOST_ADDRESS;
        public const int PORT = 25565;

        protected List<JObject> scriptedEvents;
        public readonly T netBase;

        int ticks = 0;

        public DriverBase(string scriptPath, T netBase)
        {
            this.netBase = netBase;
            scriptedEvents = ReadScriptFile(scriptPath);
        }

        protected List<JObject> ReadScriptFile(string path)
        {
            string json = File.ReadAllText(path);
            return JArray.Parse(json).Cast<JObject>().ToList();
        }

        public virtual void TryTick()
        {
            if (!netBase.ShouldTick) return;
            ticks++;
            netBase.ReadEvents(ticks);
        }

        public virtual void Update()
        {
            // We discard read events, since they're already logged
            netBase.ReadEvents(ticks);
            if (!netBase.Started) return;
            // Go through scripted events and if they're ready to go, pretend
            // the user initiated them
            foreach (JObject message in TimberNetBase.PopEventsForTick(netBase.TickCount, scriptedEvents, TimberNetBase.GetTick))
            {
                netBase.TryUserInitiatedEvent(message);
            }
        }
    }

    internal class ClientDriver : DriverBase<TimberClient>
    {
        const string SCRIPT_PATH = "client.json";

        public ClientDriver() : base(SCRIPT_PATH, new TimberClient(ADDRESS, PORT))
        {
        }
    }

    internal class ServerDriver : DriverBase<TimberServer>
    {
        const string SCRIPT_PATH = "server.json";
        const string SAVE_PATH = "save.timber";

        public ServerDriver() : base(SCRIPT_PATH, new TimberServer(PORT, 
            () => File.ReadAllBytesAsync(SAVE_PATH), null))
        {
            
        }

        public override void Update()
        {
            netBase.Update();
            if (netBase.ClientCount == 0) return;
            base.Update();
        }

        public override void TryTick()
        {
            base.TryTick();
            netBase.SendHeartbeat();
        }
    }
}
