using Newtonsoft.Json.Linq;

namespace TimberNet
{
    public delegate void TargetSpeedChanged(int speed);

    internal abstract class DriverBase<T> where T : TimberNetBase
    {
        public const string LOCALHOST = "127.0.0.1";
        public const int PORT = 25565;

        protected List<JObject> scriptedEvents;
        public readonly T netBase;

        public event TargetSpeedChanged? OnTargetSpeedChanged;

        protected int ticks = 0;

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

        private void ReadEvents()
        {
            var events = netBase.ReadEvents(ticks);
            foreach (JObject message in events)
            {
                ProcessEvent(message);
            }
        }

        protected virtual void ProcessEvent(JObject message)
        {
            if (TimberNetBase.GetType(message) == "SpeedSetEvent")
            {
                int speed = message["speed"]!.ToObject<int>();
                OnTargetSpeedChanged?.Invoke(speed);
            }
        }

        public virtual void TryTick()
        {
            if (!netBase.ShouldTick) return;
            ticks++;
            ReadEvents();
        }

        public virtual void Update()
        {
            // We discard read events, since they're already logged
            ReadEvents();
            if (!netBase.Started) return;
            // Go through scripted events and if they're ready to go, pretend
            // the user initiated them
            foreach (JObject message in TimberNetBase.PopEventsForTick(netBase.TickCount, scriptedEvents, TimberNetBase.GetTick))
            {
                netBase.DoUserInitiatedEvent(message);
            }
        }
    }

    internal class ClientDriver : DriverBase<TimberClient>
    {
        const string SCRIPT_PATH = "client.json";

        public ClientDriver() : base(SCRIPT_PATH, new TimberClient(new TCPClientWrapper(LOCALHOST, PORT)))
        {
        }
    }

    internal class ServerDriver : DriverBase<TimberServer>
    {
        const string SCRIPT_PATH = "server.json";
        const string SAVE_PATH = "save.timber";

        public ServerDriver() : base(SCRIPT_PATH, new TimberServer(new TCPListenerWrapper(PORT), 
            () => File.ReadAllBytesAsync(SAVE_PATH), CreateInitEvent()))
        {
            
        }

        private static Func<JObject> CreateInitEvent()
        {
            // We create the event out of JSON manually because
            // we want to test CreateInitEvent, rather than just putting it in the script
            JObject initEvent = new JObject();
            initEvent["$type"] = "TimberModTest.RandomStateSetEvent, TimberModTest";
            initEvent["type"] = "RandomStateSetEvent";
            initEvent["ticksSinceLoad"] = 0;
            initEvent["timeInFixedSeconds"] = 0.0;
            initEvent["seed"] = 1234;
            return () => initEvent;
        }

        protected override void ProcessEvent(JObject message)
        {
            base.ProcessEvent(message);
            netBase.DoUserInitiatedEvent(message);
        }

        public override void Update()
        {
            netBase.Update();
            if (netBase.ClientCount == 0) return;
            base.Update();
        }

        public override void TryTick()
        {
            if (!netBase.ShouldTick) return;
            //if (ticks == 0)
            //{
            //    netBase.DoUserInitiatedEvent(CreateInitEvent()());
            //}
            base.TryTick();
        }
    }
}
