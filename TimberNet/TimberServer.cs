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
        private readonly ConcurrentDictionary<TcpClient, ConcurrentQueue<JObject>> queuedMessages =
            new ConcurrentDictionary<TcpClient, ConcurrentQueue<JObject>>();

        private readonly TcpListener listener;
        private readonly Func<Task<byte[]>> mapProvider;
        private readonly Func<JObject>? initEventProvider;

        public int ClientCount => clients.Count;

        public TimberServer(int port, Func<Task<byte[]>> mapProvider, Func<JObject>? initEventProvider)
        {
            listener = new TcpListener(IPAddress.Any, port);
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
                while (!IsStopped)
                {
                    TcpClient client;
                    try
                    {
                        client = listener.AcceptTcpClient();
                    } catch
                    {
                        continue;
                    }
                    Task.Run(async () =>
                    {
                        await SendMap(client);
                        SendState(client);
                        if (initEventProvider != null)
                        {
                            JObject initEvent = initEventProvider();
                            DoUserInitiatedEvent(initEvent);
                        }
                        StartListening(client, false);
                        FinishQueuing(client);
                    });
                }
            });
        }

        private void StartQueuing (TcpClient client)
        {
            lock (queuedMessages)
            {
                queuedMessages.TryAdd(client, new ConcurrentQueue<JObject>());
                clients.Add(client);
            }
        }

        private void FinishQueuing(TcpClient client)
        {
            lock(queuedMessages)
            {
                if (queuedMessages.TryGetValue(client, out ConcurrentQueue<JObject> queue))
                {
                    while (queue.TryDequeue(out JObject message))
                    {
                        SendEvent(client, message);
                    }
                    queuedMessages.TryRemove(client, out _);
                }
                else
                {
                    Log("Warning! Missing client!");
                }
            }
        }

        private async Task SendMap(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            byte[] mapBytes = await mapProvider();

            // Start recording messages as soon as the map is saved,
            // while the map is sending
            StartQueuing(client);


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
            // Send directly - don't queue
            SendEvent(client, message);
        }

        public override void DoUserInitiatedEvent(JObject message)
        {
            base.DoUserInitiatedEvent(message);
            SendEventToClients(message);
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
            // Make sure we're not running this while a client is being
            // setup to start or stop queueing
            lock (queuedMessages)
            {
                clients.ForEach(client =>
                {
                    QueueOrSentToClient(client, message);
                });
            }
        }

        private void QueueOrSentToClient(TcpClient client, JObject message)
        {
            if (!client.Connected) return;

            if (queuedMessages.TryGetValue(client, out ConcurrentQueue<JObject> queue))
            {
                queue.Enqueue(message);
            }
            else
            {
                SendEvent(client, message);
            }
        }

        public override void Close()
        {
            base.Close();
            try
            {
                listener.Stop();
            }
            catch { }  
        }

        public void SendHeartbeat()
        {
            JObject message = new JObject();
            message[TICKS_KEY] = TickCount;
            message[TYPE_KEY] = HEARTBEAT_EVENT;
            // Simulate the user doing this
            DoUserInitiatedEvent(message);
        }
    }
}
