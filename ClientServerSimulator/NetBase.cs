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

        public event MessageReceived? OnMessageReceived;

        private readonly ConcurrentQueue<string> toProcess = new();

        public int TickCount { get; private set; }

        protected List<JObject> script;


        public NetBase(string scriptPath)
        {
            script = ReadFile(scriptPath);
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

        protected void InsertInScript(JObject message)
        {
            int tick = GetTick(message);
            int index = script.FindIndex(m => GetTick(m) >= tick);
            if (index == -1)
                script.Add(message);
            else
                script.Insert(index, message);
        }

        protected void UpdateSending(TcpClient client)
        {
            while (script.Count > 0)
            {
                JObject message = script[0];
                int delay = message[TICKS_KEY]!.ToObject<int>();
                if (delay > TickCount)
                    break;

                script.RemoveAt(0);
                string json = message.ToString();
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                byte[] header = BitConverter.GetBytes(buffer.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(header);

                client.GetStream().Write(header, 0, header.Length);
                client.GetStream().Write(buffer, 0, buffer.Length);
            }
        }

        protected void StartListening(TcpClient client)
        {
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
                OnMessageReceived?.Invoke(message);
            }
        }


        public virtual void TryTick()
        {
            TickCount++;
            ProcessQueue();
        }
    }
}
