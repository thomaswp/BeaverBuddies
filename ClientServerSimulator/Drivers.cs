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

namespace ClientServerSimulator
{
    internal abstract class DriverBase<T> where T : TimberNetBase
    {
        public const string ADDRESS = TimberNetBase.HOST_ADDRESS;
        public const int PORT = 25565;

        protected List<JObject> scriptedEvents;
        public readonly T netBase;

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

        public void Update()
        {
            netBase.Update();
            if (!netBase.Started) return;
            // Go through scripted events and if they're ready to go, pretend
            // the user initiated them
            TimberNetBase.ProcessEventsForTick(netBase.TickCount, scriptedEvents, 
                message => netBase.TryUserInitiatedEvent(message));
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

        public ServerDriver() : base(SCRIPT_PATH, new TimberServer(PORT))
        {
        }
    }
}
