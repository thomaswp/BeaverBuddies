using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace TimberModTest
{
    public interface EventIO
    {
        List<ReplayEvent> ReadEvents();

        void WriteEvents(params ReplayEvent[] events);

        void Close();
    }

    //class EventWrapper
    //{
    //    public string type;
    //    public ReplayEvent data;

    //    private static Type[] types = {
    //        typeof(BuildingPlacedEvent)
    //    };

    //    static EventWrapper FromEvent(ReplayEvent replayEvent)
    //    {
    //        return new EventWrapper()
    //        {
    //            type = replayEvent.GetType().Name,
    //            data = replayEvent
    //        };
    //    }

    //    public string ToJSON()
    //    {
    //        return JsonConvert.SerializeObject(this);
    //    }

    //    static ReplayEvent ToEvent(string json)
    //    {
    //        EventWrapper wrapper = JsonConvert.DeserializeObject<EventWrapper>(json);

    //    }
    //}

    class JsonSettings : JsonSerializerSettings
    {
        public JsonSettings()
        {
            Formatting = Formatting.Indented;
            TypeNameHandling = TypeNameHandling.All;
        }
    }
    
    public class FileWriteIO : EventIO
    {
        private JsonSerializerSettings settings;
        private string filePath;

        public FileWriteIO(string filePath)
        {
            this.filePath = filePath;
            settings = new JsonSettings();
            WriteToFile("[");
        }

        
        public List<ReplayEvent> ReadEvents()
        {
            throw new NotImplementedException();
        }

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
        private JsonSerializerSettings settings;
        private List<ReplayEvent> events = new();
        private string filePath;

        public FileReadIO(string filePath)
        {
            this.filePath = filePath;
            settings = new JsonSettings();

            ReadFileEvents();
        }

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

        public List<ReplayEvent> ReadEvents()
        {
            var list = new List<ReplayEvent>(events);
            events.Clear();
            return list;
        }

        public void WriteEvents(params ReplayEvent[] events)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {

        }
    }

}
