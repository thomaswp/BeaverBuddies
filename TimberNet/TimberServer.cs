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

        private readonly List<ISocketStream> clients = new List<ISocketStream>();
        private readonly ConcurrentDictionary<ISocketStream, ConcurrentQueue<JObject>> queuedMessages =
            new ConcurrentDictionary<ISocketStream, ConcurrentQueue<JObject>>();

        private readonly ISocketListener listener;

        private Func<Task<byte[]>> mapProvider;
        private Func<JObject>? initEventProvider;

        public int ClientCount => clients.Count;

        private string? errorMessage = null;
        public bool IsAcceptingClients => errorMessage == null;

        public TimberServer(ISocketListener listener, Func<Task<byte[]>> mapProvider, Func<JObject>? initEventProvider)
        {
            this.listener = listener;
            this.mapProvider = mapProvider;
            this.initEventProvider = initEventProvider;
        }

        public void UpdateProviders(Func<Task<byte[]>> mapProvider, Func<JObject>? initEventProvider)
        {
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
                // TODO: I have a suspicion that this while plus the catch/continue below
                // is responsible for the server hanging sometimes on a connection that's dropped.
                // Logging now to see if I can catch it.
                while (!IsStopped)
                {
                    ISocketStream client;
                    try
                    {
                        Log("Accepting client...");
                        client = listener.AcceptClient();
                    } catch (Exception e)
                    {
                        Log("Error accepting client.");
                        Log(e.StackTrace);
                        continue;
                    }
                    Task.Run(async () =>
                    {
                        if (!IsAcceptingClients)
                        {
                            SendErrorMessage(client);
                            client.Close();
                            return;
                        }

                        await SendMap(client);
                        SendState(client);
                        if (initEventProvider != null)
                        {
                            JObject initEvent = initEventProvider();
                            // Send the event before finishing queueing
                            // so it is guaranteed to arrive first.
                            // (This also sends it to other clients.)
                            DoUserInitiatedEvent(initEvent, true);
                        }
                        FinishQueuing(client);

                        // This must come last - it is an infinite loop
                        // until the client disconnects
                        StartListening(client, false);
                    });
                }
            });
        }

        public void StopAcceptingClients(string errorMessage)
        {
            this.errorMessage = errorMessage;
        }

        private void StartQueuing (ISocketStream client)
        {
            lock (queuedMessages)
            {
                queuedMessages.TryAdd(client, new ConcurrentQueue<JObject>());
                clients.Add(client);
            }
        }

        private void FinishQueuing(ISocketStream client)
        {
            // Log("finishing queuing");
            lock(queuedMessages)
            {
                if (queuedMessages.TryGetValue(client, out ConcurrentQueue<JObject> queue))
                {
                    // Log($"Found {queue.Count} messages");
                    while (queue.TryDequeue(out JObject message))
                    {
                        // Log(message.ToString());
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

        private void SendErrorMessage(ISocketStream client)
        {
            SendLength(client, 0);
            byte[] bytes = Encoding.UTF8.GetBytes(errorMessage);
            SendLength(client, bytes.Length);
            // TODO: Not sure this makes sense for Steam
            client.Write(bytes, 0, bytes.Length);
        }

        private async Task SendMap(ISocketStream client)
        { 
            Task<byte[]> task = mapProvider();
            Log("Waiting for map...");
            byte[] mapBytes = await task;
            Log($"Sending map with length {mapBytes.Length}");

            // TODO: This may happen a bit early - it seems possible for
            // events from a prior frame to get queued. Maybe just need to filter
            // them on the client side.
            // Start recording messages as soon as the map is saved,
            // while the map is sending
            StartQueuing(client);


            SendLength(client, mapBytes.Length);
            
            // Send bytes in chunks
            int chunkSize = MAX_BUFFER_SIZE;
            for (int i = 0; i < mapBytes.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, mapBytes.Length - i);
                client.Write(mapBytes, i, length);
            }

            Log($"Sent map with length {mapBytes.Length} and Hash: {GetHashCode(mapBytes).ToString("X8")}");
        }

        private void SendState(ISocketStream client)
        {
            JObject message = new JObject();
            message[TICKS_KEY] = 0;
            message[TYPE_KEY] = SET_STATE_EVENT;
            message["hash"] = Hash;
            // Send directly - don't queue
            SendEvent(client, message);
        }

        void DoUserInitiatedEvent(JObject message, bool sendNow)
        {
            base.DoUserInitiatedEvent(message);
            SendEventToClients(message, sendNow);
        }

        public override void DoUserInitiatedEvent(JObject message)
        {
            DoUserInitiatedEvent(message, false);
        }

        private void SendEventToClients(JObject message, bool sendNow)
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
                    if (sendNow)
                    {
                        SendEvent(client, message);
                    }
                    else
                    {
                        QueueOrSentToClient(client, message);
                    }
                });
            }
        }

        private void QueueOrSentToClient(ISocketStream client, JObject message)
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
                clients.ForEach(client => client.Close());
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            try
            {
                listener.Stop();
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }  
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
