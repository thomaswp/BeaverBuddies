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

        public override void Start()
        {
            client.Connect(ADDRESS, PORT);
            // Connect a TCP socket at the address
            Thread listen = new Thread(new ThreadStart(() => StartListening(client)));
            UpdateSending(client);
        }


        public override void Close()
        {
            client.Close();
        }

        public override void TryTick()
        {
            // TODO: Wait if caught up to client
            base.TryTick();
            UpdateSending(client);
        }
    }
}
