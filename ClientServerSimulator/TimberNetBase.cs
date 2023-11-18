using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace ClientServerSimulator
{
    public abstract class TimberNetBase
    {
        public const int HEADER_SIZE = 4;
        public const string TICKS_KEY = "ticksSinceLoad";
        public const string TYPE_KEY = "type";
        public const string SET_STATE_EVENT = "SetState";
        public const int MAX_BUFFER_SIZE = 8192; // 8K
        public const string HOST_ADDRESS = "127.0.0.1";

        public delegate void MessageReceived(string message);
        public delegate void MapReceived(byte[] mapBytes);

        public event MessageReceived? OnLog;
        public event MapReceived? OnMapReceived;

        private readonly ConcurrentQueue<string> receivedEventQueue = new();
        private readonly ConcurrentQueue<string> logQueue = new();
        private byte[]? mapBytes = null;

        public int Hash { get; private set; }

        public int TickCount { get; private set; }

        public bool Started { get; private set; }

        protected List<JObject> receivedEvents = new List<JObject>();

        public abstract void Close();

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

        protected int GetTick(JObject message)
        {
            if (message[TICKS_KEY] == null)
                throw new Exception($"Message does not contain {TICKS_KEY} key");
            return message[TICKS_KEY]!.ToObject<int>();
        }

        protected string GetType(JObject message)
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

        public static void ProcessEventsForTick(int tick, List<JObject> events, Action<JObject> callback)
        {
            while (events.Count > 0)
            {
                JObject message = events[0];
                int delay = message[TICKS_KEY]!.ToObject<int>();
                if (delay > tick)
                    break;

                events.RemoveAt(0);
                callback(message);
            }

        }

        protected void ProcessEvents(List<JObject> events, Action<JObject> callback)
        {
            if (events.Count == 0) return;
            JObject firstEvent = events[0];
            int firstEventTick = GetTick(firstEvent);
            if (firstEventTick < TickCount)
                Log($"Warning: late event {GetType(firstEvent)}: {firstEventTick} < {TickCount}");

            ProcessEventsForTick(TickCount, events, callback);
        }

        public virtual bool TryUserInitiatedEvent(JObject message)
        {
            AcceptEvent(message);
            return true;
        }

        protected virtual void ProcessReceivedEvent(JObject message)
        {
            AcceptEvent(message);
        }

        protected virtual void AcceptEvent(JObject message)
        {
            if (GetType(message) == SET_STATE_EVENT)
            {
                Hash = message["hash"]!.ToObject<int>();
            }
            else
            {
                Hash = HashCode.Combine(Hash, message.ToString());
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
            client.GetStream().Write(buffer, 0, buffer.Length);
        }

        protected void StartListening(TcpClient client, bool isClient)
        {
            //Log("Client connected");
            var stream = client.GetStream();
            byte[] headerBuffer = new byte[HEADER_SIZE];
            int messageCount = 0;
            while (client.Connected)
            {
                stream.Read(headerBuffer, 0, headerBuffer.Length);
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

        protected int GetHashCode(byte[] bytes)
        {
            int code = 0;
            foreach (byte b in bytes)
            {
                code = HashCode.Combine(code, b);
            }
            return code;
        }

        protected void AddFileToHash(byte[] bytes)
        {
            Hash = HashCode.Combine(Hash, GetHashCode(bytes));
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
                ReceiveEvent(JObject.Parse(message));
            }
        }

        protected virtual void ReceiveEvent(JObject message)
        {
            InsertInScript(message, receivedEvents);
        }

        private void UpdateLogs()
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

        public virtual void Update()
        {
            UpdateLogs();
            if (!Started) return;
            ProcessReceivedMap();
            ProcessReceivedEventsQueue();
            ProcessEvents(receivedEvents, ProcessReceivedEvent);
        }

        protected virtual bool ShouldTick => true;

        public virtual bool TryTick()
        {
            Update();
            if (!Started) return false;
            if (!ShouldTick) return false;
            TickCount++;
            Update();
            return true;
        }
    }
}
