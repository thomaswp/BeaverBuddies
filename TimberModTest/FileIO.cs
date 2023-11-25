using Mono.Cecil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using TimberNet;

namespace TimberModTest
{

    class JsonSettings : JsonSerializerSettings
    {
        public JsonSettings()
        {
            Formatting = Formatting.Indented;
            TypeNameHandling = TypeNameHandling.All;
        }

        public static readonly JsonSettings Default = new JsonSettings();

        public static T Deserialize<T>(string json)
        {
            return (T)JsonConvert.DeserializeObject(json, Default);
        }

        public static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, Default);
        }
    }
    
    public class FileWriteIO : EventIO
    {
        public bool PlayUserEvents => true;
        // Will probably never happen, since nothing to replay
        public bool RecordReplayedEvents => true;
        public bool IsOutOfEvents => false;

        private JsonSerializerSettings settings;
        private string filePath;

        public FileWriteIO(string filePath)
        {
            this.filePath = filePath;
            settings = new JsonSettings();
            WriteToFile("[");
        }

        
        public List<ReplayEvent> ReadEvents(int ticksSinceLoad)
        {
            return new List<ReplayEvent>();
        }

        public void Update() { }

        public void WriteEvents(params ReplayEvent[] events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                string json = JsonConvert.SerializeObject(e, settings);
                WriteToFile(json + ",");
            }
        }

        public void Close()
        {
            WriteToFile("]");
        }
       
        private static ReaderWriterLock locker = new ReaderWriterLock();

        public void WriteToFile(string text)
        {
            try
            {
                locker.AcquireWriterLock(1000);
                File.AppendAllText(filePath, text );
            }
            finally
            {
                locker.ReleaseWriterLock();
            }
        }
    }

    public class FileReadIO : EventIO
    {
        public bool PlayUserEvents => true;
        // Shouldn't need to record anything
        public bool RecordReplayedEvents => false;
        public bool IsOutOfEvents => events.Count == 0;

        private JsonSerializerSettings settings;
        private List<ReplayEvent> events = new();
        private string filePath;

        public FileReadIO(string filePath)
        {
            this.filePath = filePath;
            settings = new JsonSettings();

            ReadFileEvents();
        }

        public void Update() { }

        void ReadFileEvents()
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var array = JArray.Parse(json);
                    events = array.Select(o => JsonConvert.DeserializeObject(o.ToString(), settings) as ReplayEvent).ToList();
                }
                catch (Exception e)
                {
                    Plugin.Log("Failed to load json: " + e.Message);
                }
            }

        }

        public List<ReplayEvent> ReadEvents(int ticksSinceLoad)
        {
            return TimberNetBase.PopEventsForTick(ticksSinceLoad, events, e => e.ticksSinceLoad);
        }

        public void WriteEvents(params ReplayEvent[] events)
        {
            //throw new NotImplementedException();
        }

        public void Close()
        {

        }
    }

}
