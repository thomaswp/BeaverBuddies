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
            OnMessageReceived += Server_OnMessageReceived;
        }

        private void Server_OnMessageReceived(string message)
        {
            JObject obj = new JObject(message);
            obj[TICKS_KEY] = TickCount;
            InsertInScript(obj);
        }

        public override void Start()
        {
            listener.Start();
            
            Task.Run(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    clients.Add(client);
                    Task.Run(() => StartListening(client));
                }
            });
        }


        public override void Close()
        {
            listener.Stop();
        }

        public override void TryTick()
        {
            base.TryTick();
            clients.ForEach(client => UpdateSending(client));
            // TODO: Send a heartbeat
        }
    }
}
