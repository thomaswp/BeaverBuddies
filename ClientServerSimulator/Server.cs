using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace ClientServerSimulator
{
    internal class Server : NetBase
    {
        const string SCRIPT_PATH = "server.json";

        private readonly List<TcpClient> clients = new();

        readonly TcpListener listener;

        public Server() : base(SCRIPT_PATH)
        {
            listener = new TcpListener(IPAddress.Parse(ADDRESS), PORT);
        }

        protected override void ReceiveEvent(JObject message)
        {
            message[TICKS_KEY] = TickCount;
            base.ReceiveEvent(message);
        }

        public override void Start()
        {
            listener.Start();
            Log("Server started listening");
            
            Task.Run(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    // TODO: Send current state!
                    clients.Add(client);
                    Task.Run(() => StartListening(client));
                }
            });
        }

        protected override void DoEvent(JObject message)
        {
            base.DoEvent(message);
            
            clients.ForEach(client => SendEvent(client, message));
        }

        public override void Close()
        {
            listener.Stop();
        }

        public override void TryTick()
        {
            base.TryTick();
            if (TickCount % 10 == 0)
            {
                JObject message = new JObject();
                message[TICKS_KEY] = TickCount;
                message["type"] = "Heartbeat";
                ProcessMyEvent(message);
            }
        }
    }
}
