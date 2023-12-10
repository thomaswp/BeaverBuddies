using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TimberNet
{
    public abstract class TimberNetBase
    {
        public const int HEADER_SIZE = 4;
        public const string TICKS_KEY = "ticksSinceLoad";
        public const string TYPE_KEY = "type";
        public const string SET_STATE_EVENT = "SetState";
        public const string HEARTBEAT_EVENT = "Heartbeat";
        public const int MAX_BUFFER_SIZE = 8192; // 8K

        public delegate void MessageReceived(string message);
        public delegate void MapReceived(byte[] mapBytes);

        public event MessageReceived? OnLog;
        public event MapReceived? OnMapReceived;

        private readonly ConcurrentQueue<string> receivedEventQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private byte[]? mapBytes = null;

        public bool IsStopped { get; private set; } = false;

        public int Hash { get; private set; } = 17;

        public int TickCount { get; private set; }

        public int TicksBehind
        {
            get
            {
                if (receivedEvents.Count == 0)
                    return 0;
                return Math.Max(0, GetTick(receivedEvents.Last()) - TickCount);
            }
        }

        public bool Started { get; private set; }

        public virtual bool ShouldTick => Started;

        protected List<JObject> receivedEvents = new List<JObject>();

        public virtual void Close()
        {
            IsStopped = true;
        }

        public TimberNetBase()
        {
            Log("Started");
        }

        protected void Log(string message)
        {
            Log(message, TickCount, Hash);
        }

        protected void Log(string message, int ticks, int hash)
        {

            logQueue.Enqueue($"T{ticks.ToString("D4")} [{hash.ToString("X8")}] : {message}");
        }

        public virtual void Start()
        {
            Started = true;
        }

        public static int GetTick(JObject message)
        {
            if (message[TICKS_KEY] == null)
                throw new Exception($"Message does not contain {TICKS_KEY} key");
            return message[TICKS_KEY]!.ToObject<int>();
        }

        public static string GetType(JObject message)
        {
            var type = message["type"];
            if (type == null)
                throw new Exception($"Message does not contain type key");
            return type.ToObject<string>()!;
        }

        protected void InsertInScript(JObject message, List<JObject> script)
        {
            int tick = GetTick(message);
            int index = script.FindIndex(m => GetTick(m) > tick);
            if (index == -1)
                script.Add(message);
            else
                script.Insert(index, message);
        }

        public static List<T> PopEventsForTick<T>(int tick, List<T> events, Func<T, int> getTick)
        {
            List<T> list = new List<T>();
            while (events.Count > 0)
            {
                T message = events[0];
                int delay = getTick(message);
                if (delay > tick)
                    break;

                events.RemoveAt(0);
                list.Add(message);
            }
            return list;
        }

        private List<JObject> PopEventsToProcess(List<JObject> events)
        {
            if (events.Count == 0) return new List<JObject>();
            JObject firstEvent = events[0];
            int firstEventTick = GetTick(firstEvent);
            if (firstEventTick < TickCount)
                Log($"Warning: late event {GetType(firstEvent)}: {firstEventTick} < {TickCount}");

            return PopEventsForTick(TickCount, events, GetTick);
        }

        /**
         * Process an event that the user initiated.
         */
        public virtual void DoUserInitiatedEvent(JObject message)
        {
            AddEventToHash(message);
        }

        /**
        * Process a validated event from a peer that is ready to happen on
        * the Update() thread.
        */
        protected virtual void ProcessReceivedEvent(JObject message)
        {
        }

        protected void AddEventToHash(JObject message)
        {
            if (GetType(message) == SET_STATE_EVENT)
            {
                Hash = message["hash"]!.ToObject<int>();
            }
            else
            {
                AddToHash(message.ToString());
            }
            Log($"Event: {GetType(message)}");
        }

        protected void SendLength(NetworkStream stream, int length)
        {
            byte[] buffer = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            stream.Write(buffer, 0, buffer.Length);
        }

        protected void SendEvent(TcpClient client, JObject message)
        {
            Log($"Sending: {GetType(message)} for tick {GetTick(message)}");
            string json = message.ToString();
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            var stream = client.GetStream();
            SendLength(stream, buffer.Length);
            try
            {
                client.GetStream().Write(buffer, 0, buffer.Length);
            } catch (Exception e)
            {
                Log($"Error sending event: {e.Message}");
            }
        }

        protected void StartListening(TcpClient client, bool isClient)
        {
            //Log("Client connected");
            var stream = client.GetStream();
            byte[] headerBuffer = new byte[HEADER_SIZE];
            int messageCount = 0;
            while (client.Connected && !IsStopped)
            {
                try
                {
                    stream.Read(headerBuffer, 0, headerBuffer.Length);
                } catch
                {
                    break;
                }
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(headerBuffer);

                int messageLength = BitConverter.ToInt32(headerBuffer, 0);

                // First message is always the file
                if (messageCount == 0 && isClient)
                {
                    ReceiveFile(stream, messageLength);
                    messageCount++;
                    continue;
                }
                byte[] buffer = new byte[messageLength];
                stream.Read(buffer, 0, messageLength);

                string message = Encoding.UTF8.GetString(buffer);
                receivedEventQueue.Enqueue(message);
                messageCount++;
            }
        }

        public static int CombineHash(int h1, int h2)
        {
            return h1 * 31 + h2;
        }

        private void AddToHash(string str)
        {
            AddToHash(Encoding.UTF8.GetBytes(str));
        }

        private void AddToHash(byte[] bytes)
        {
            Hash = CombineHash(Hash, GetHashCode(bytes));
        }

        public static int GetHashCode(byte[] bytes)
        {
            int code = 0;
            foreach (byte b in bytes)
            {
                code = CombineHash(code, b);
            }
            return code;
        }

        private void AddFileToHash(byte[] bytes)
        {
            AddToHash(bytes);
        }

        private void ReceiveFile(NetworkStream stream, int messageLength)
        {
            int totalBytesRead = 0;
            MemoryStream ms = new MemoryStream();
            while (totalBytesRead < messageLength)
            {
                int bytesToRead = Math.Min(messageLength - totalBytesRead, MAX_BUFFER_SIZE);
                byte[] buffer = new byte[bytesToRead];
                //Log($"{buffer.Length}, {GetHashCode(buffer).ToString("X8")}");
                int bytesRead = stream.Read(buffer, 0, bytesToRead);
                totalBytesRead += bytesRead;
                ms.Write(buffer, 0, bytesRead);
            }
            byte[] mapBytes = ms.ToArray();
            AddFileToHash(mapBytes);
            Log($"Received map with length {mapBytes.Length} and Hash: {GetHashCode(mapBytes).ToString("X8")}");
            this.mapBytes = mapBytes;
        }

        private void ProcessReceivedEventsQueue()
        {
            while (receivedEventQueue.TryDequeue(out string? message))
            {
                try
                {
                    ReceiveEvent(JObject.Parse(message));
                } catch (Exception e)
                {
                    Log($"Error receiving event: {e.Message}");
                }
            }
        }

        /**
         * Called when an event is received from a connected Net
         * and ready to be added to the queue for processing.
         */
        protected virtual void ReceiveEvent(JObject message)
        {
            InsertInScript(message, receivedEvents);
        }

        private void ProcessLogs()
        {
            while (logQueue.TryDequeue(out string? log))
            {
                OnLog?.Invoke(log);
            }
        }

        private void ProcessReceivedMap()
        {
            if (mapBytes == null) return;
            OnMapReceived?.Invoke(mapBytes);
            mapBytes = null;
        }

        /**
         * Updates, processing queued logs, maps and events.
         */
        public void Update()
        {
            ProcessLogs();
            if (!Started) return;
            ProcessReceivedMap();
            ProcessReceivedEventsQueue();

        }

        private List<JObject> FilterEvents(List<JObject> events)
        {
            return events.Where(ShouldReadEvent).ToList();
        }

        private bool ShouldReadEvent(JObject message)
        { 
            string type = GetType(message);
            return !(type == SET_STATE_EVENT || type == HEARTBEAT_EVENT);
        }

        /**
         * Reads received events that should be processed by the game
         * and deletes and returns.
         * Will call update before processing events.
         */
        public virtual List<JObject> ReadEvents(int ticksSinceLoad)
        {
            TickCount = ticksSinceLoad;
            Update();
            List<JObject> toProcess = PopEventsToProcess(receivedEvents);
            toProcess.ForEach(e => ProcessReceivedEvent(e));
            return FilterEvents(toProcess);
        }
    }
}
