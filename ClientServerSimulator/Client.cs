using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ClientServerSimulator
{
    public class Client : NetBase
    {
        const string SCRIPT_PATH = "client.json";

        private readonly TcpClient client;

        public Client() : base(SCRIPT_PATH)
        {
            client = new TcpClient();
        }

        protected override void ProcessMyEvent(JObject message)
        {
            SendEvent(client, message);
            // Don't actually do the event - wait for the server to confirm
            // w/ adjusted Tick
        }

        public override void Start()
        {
            client.Connect(ADDRESS, PORT);
            // Connect a TCP socket at the address
            Thread listen = new Thread(new ThreadStart(() => StartListening(client)));
            Update();
        }


        public override void Close()
        {
            client.Close();
        }

        public override void TryTick()
        {
            // Don't process if we have no received events
            if (receivedEvents.Count == 0) return;
            base.TryTick();
        }
    }
}
