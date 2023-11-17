using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClientServerSimulator
{
    public abstract class NetBase
    {
        public const int PORT = 25565;
        public const string ADDRESS = "127.0.0.1";
        public const int HEADER_SIZE = 4;
        public const string TICKS_KEY = "ticksSinceLoad";
        public const string TYPE_KEY = "type";
        public const string SET_STATE_EVENT = "SetState";
        public const int MAX_BUFFER_SIZE = 8192; // 8K

        public delegate void MessageReceived(string message);

        public event MessageReceived? OnLog;

        private readonly ConcurrentQueue<string> toProcess = new();
        private readonly ConcurrentQueue<string> logQueue = new();
        public int Hash { get; private set; }

        public int TickCount { get; private set; }

        public bool Started { get; private set; }

        protected List<JObject> scriptedEvents;
        protected List<JObject> receivedEvents = new List<JObject>();


        public NetBase(string scriptPath)
        {
            scriptedEvents = ReadScriptFile(scriptPath);
            Log("Started");
        }

        protected void Log(string message)
        {
            Log(message, TickCount, Hash);
        }

        protected void Log(string message, int ticks)
        {
            Log(message, ticks, Hash);
        }

        protected void Log(string message, int ticks, int hash)
        {

            logQueue.Enqueue($"T{ticks.ToString("D4")} [{hash.ToString("X8")}] : {message}");
        }

        public virtual void Start()
        {
            Started = true;
        }

        public abstract void Close();

        protected List<JObject> ReadScriptFile(string path)
        {
            string json = File.ReadAllText(path);
            return JArray.Parse(json).Cast<JObject>().ToList();
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

        private void ProcessEvents(List<JObject> events, Action<JObject> callback)
        {
            while (events.Count > 0)
            {
                JObject message = events[0];
                int delay = message[TICKS_KEY]!.ToObject<int>();
                if (delay > TickCount)
                    break;

                if (delay < TickCount)
                    Log($"Warning: late event {message["type"]}: {delay} < {TickCount}");

                events.RemoveAt(0);
                callback(message);
            }
        }


        protected virtual void ProcessMyEvent(JObject message)
        {
            DoEvent(message);
        }

        protected virtual void ProcessReceivedEvent(JObject message)
        {
            DoEvent(message);
        }

        protected virtual void DoEvent(JObject message)
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
                toProcess.Enqueue(message);
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
        }

        private void ProcessQueue()
        {
            while (toProcess.TryDequeue(out string? message))
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

        public void Update()
        {
            UpdateLogs();
            if (!Started) return;
            ProcessQueue();
            ProcessEvents(receivedEvents, ProcessReceivedEvent);
            ProcessEvents(scriptedEvents, ProcessMyEvent);
        }

        protected virtual bool ShouldTick => true;

        public virtual void TryTick()
        {
            Update();
            if (!Started) return;
            if (ShouldTick)
            {
                TickCount++;
                Update();
            }
        }
    }
}
