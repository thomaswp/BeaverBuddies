using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ClientServerSimulator
{
    public abstract class NetBase
    {
        public const int PORT = 25565;
        public const string ADDRESS = "127.0.0.1";
        public const int HEADER_SIZE = 4;
        public const string TICKS_KEY = "ticksSinceLoad";

        public delegate void MessageReceived(string message);

        public event MessageReceived? OnLog;

        private readonly ConcurrentQueue<string> toProcess = new();
        public int Hash { get; private set; }

        public int TickCount { get; private set; }

        protected List<JObject> scriptedEvents;
        protected List<JObject> receivedEvents = new List<JObject>();


        public NetBase(string scriptPath)
        {
            scriptedEvents = ReadFile(scriptPath);
            Log("Started");
        }

        protected void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        public abstract void Start();
        public abstract void Close();

        protected List<JObject> ReadFile(string path)
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
            Hash = HashCode.Combine(Hash, message.ToString());
            Log($"Event at T{GetTick(message).ToString("D4")} [{Hash.ToString("X8")}]: {message["type"]}");
        }

        protected void SendEvent(TcpClient client, JObject message)
        {
            //Log($"Sending event {message["type"]} for tick {GetTick(message)}");
            string json = message.ToString();
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            byte[] header = BitConverter.GetBytes(buffer.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(header);

            client.GetStream().Write(header, 0, header.Length);
            client.GetStream().Write(buffer, 0, buffer.Length);
        }

        protected void StartListening(TcpClient client)
        {
            //Log("Client connected");
            var stream = client.GetStream();
            byte[] headerBuffer = new byte[HEADER_SIZE];
            while (client.Connected)
            {
                stream.Read(headerBuffer, 0, headerBuffer.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(headerBuffer);

                int messageLength = BitConverter.ToInt32(headerBuffer, 0);
                byte[] buffer = new byte[messageLength];
                stream.Read(buffer, 0, messageLength);

                string message = Encoding.UTF8.GetString(buffer);
                toProcess.Enqueue(message);
            }
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

        protected void Update()
        {
            ProcessQueue();
            ProcessEvents(receivedEvents, ProcessReceivedEvent);
            ProcessEvents(scriptedEvents, ProcessMyEvent);
        }

        protected virtual bool ShouldTick => true;

        public virtual void TryTick()
        {
            Update();
            if (ShouldTick)
            {
                TickCount++;
                Update();
            }
        }
    }
}
