using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Timberborn.GameSceneLoading;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using TimberModTest.Events;
using TimberNet;

namespace TimberModTest
{

    class JsonSettings : JsonSerializerSettings
    {
        public JsonSettings()
        {
            // TODO: Undo for production
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

    public class RecordToFileService : IPostLoadableSingleton
    {
        private string fileName;
        public RecordToFileService(SceneLoader sceneLoader)
        {
            var saveRef = sceneLoader.GetSceneParameters<GameSceneParameters>()?.SaveReference;
            if (saveRef != null)
            {
                fileName = saveRef.SaveName + ".json";
            }
            else
            {
                Plugin.LogError("Unknown save name");
                fileName = "test.json";
            }

        }

        public void PostLoad()
        {
            if (EventIO.Config.GetNetMode() != NetMode.Record) return;
            Plugin.Log("Recording to file");
            EventIO.Set(new FileWriteIO("Replays/" + fileName));

        }
    }
    
    public class FileWriteIO : EventIO
    {
        public bool RecordReplayedEvents => true;
        public bool ShouldSendHeartbeat => false;
        public UserEventBehavior UserEventBehavior => UserEventBehavior.Play;
        public bool IsOutOfEvents => false;
        public int TicksBehind => 0;

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
        public UserEventBehavior UserEventBehavior => UserEventBehavior.Play;
        public bool ShouldSendHeartbeat => false;
        // Shouldn't need to record anything
        public bool RecordReplayedEvents => false;
        public bool IsOutOfEvents => events.Count == 0;
        public int TicksBehind => 0;

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
                    Plugin.LogError("Failed to load json: " + e.Message);
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
