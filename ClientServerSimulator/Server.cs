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
        const string SAVE_PATH = "save.timber";

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
            base.Start();

            var bytes = File.ReadAllBytes(SAVE_PATH);
            AddFileToHash(bytes);

            listener.Start();
            Log("Server started listening");
            
            Task.Run(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    // TODO: Send current state!
                    clients.Add(client);
                    Task.Run(() =>
                    {
                        SendFile(client.GetStream(), bytes);
                        SendState(client);
                        StartListening(client, false);
                    });
                }
            });
        }

        private void SendFile(NetworkStream stream, byte[] bytes)
        {
            SendLength(stream, bytes.Length);
            
            // Send bytes in chunks
            int chunkSize = MAX_BUFFER_SIZE;
            for (int i = 0; i < bytes.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, bytes.Length - i);
                stream.Write(bytes, i, length);
            }

            Log($"Sent map with length {bytes.Length} and Hash: {GetHashCode(bytes).ToString("X8")}");
        }

        private void SendState(TcpClient client)
        {
            JObject message = new JObject();
            message[TICKS_KEY] = 0;
            message[TYPE_KEY] = SET_STATE_EVENT;
            message["hash"] = Hash;
            SendEvent(client, message);
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
