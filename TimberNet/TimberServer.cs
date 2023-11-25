using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;

namespace TimberNet
{ 

    public class TimberServer : TimberNetBase
    {

        private readonly List<TcpClient> clients = new List<TcpClient>();

        private readonly TcpListener listener;
        private readonly Func<Task<byte[]>> mapProvider;
        private readonly Func<JObject>? initEventProvider;

        public int ClientCount => clients.Count;

        public TimberServer(int port, Func<Task<byte[]>> mapProvider, Func<JObject>? initEventProvider)
        {
            listener = new TcpListener(IPAddress.Parse(HOST_ADDRESS), port);
            this.mapProvider = mapProvider;
            this.initEventProvider = initEventProvider;
        }

        protected override void ReceiveEvent(JObject message)
        {
            message[TICKS_KEY] = TickCount;
            base.ReceiveEvent(message);
        }

        public override void Start()
        {
            base.Start();

            listener.Start();
            Log("Server started listening");
            
            Task.Run(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    clients.Add(client);
                    Task.Run(async () =>
                    {
                        await SendMap(client.GetStream());
                        SendState(client);
                        if (initEventProvider != null)
                        {
                            JObject initEvent = initEventProvider();
                            TryUserInitiatedEvent(initEvent);
                        }
                        StartListening(client, false);
                    });
                }
            });
        }

        private async Task SendMap(NetworkStream stream)
        {

            byte[] mapBytes = await mapProvider();
            SendLength(stream, mapBytes.Length);
            
            // Send bytes in chunks
            int chunkSize = MAX_BUFFER_SIZE;
            for (int i = 0; i < mapBytes.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, mapBytes.Length - i);
                stream.Write(mapBytes, i, length);
            }

            Log($"Sent map with length {mapBytes.Length} and Hash: {GetHashCode(mapBytes).ToString("X8")}");
        }

        private void SendState(TcpClient client)
        {
            JObject message = new JObject();
            message[TICKS_KEY] = 0;
            message[TYPE_KEY] = SET_STATE_EVENT;
            message["hash"] = Hash;
            SendEvent(client, message);
        }

        protected override void ProcessReceivedEvent(JObject message)
        {
            base.ProcessReceivedEvent(message);
            SendEventToClients(message);
        }

        public override bool TryUserInitiatedEvent(JObject message)
        {
            if (!base.TryUserInitiatedEvent(message)) return false;
            SendEventToClients(message);
            return true;
        }

        private void SendEventToClients(JObject message)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                if (!clients[i].Connected)
                {
                    clients.RemoveAt(i);
                    i--;
                }
            }
            clients.ForEach(client => SendEvent(client, message));
        }

        public override void Close()
        {
            listener.Stop();
        }

        public void SendHeartbeat()
        {
            JObject message = new JObject();
            message[TICKS_KEY] = TickCount;
            message[TYPE_KEY] = HEARTBEAT_EVENT;
            // Simulate the user doing this
            TryUserInitiatedEvent(message);
        }
    }
}
