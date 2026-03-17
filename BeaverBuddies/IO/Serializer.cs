using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace BeaverBuddies.IO
{
    class Vector3IntConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector3Int);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);
            return new Vector3Int(array[0].Value<int>(), array[1].Value<int>(), array[2].Value<int>());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var vector = (Vector3Int)value;
            writer.WriteStartArray();
            writer.WriteValue(vector.x);
            writer.WriteValue(vector.y);
            writer.WriteValue(vector.z);
            writer.WriteEndArray();
        }
    }

    class Vector3Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector3);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);
            return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var vector = (Vector3)value;
            writer.WriteStartArray();
            writer.WriteValue(vector.x);
            writer.WriteValue(vector.y);
            writer.WriteValue(vector.z);
            writer.WriteEndArray();
        }
    }

    class JsonSettings : JsonSerializerSettings
    {
        public JsonSettings()
        {
            // TODO: Undo for production
            Formatting = Formatting.Indented;
            TypeNameHandling = TypeNameHandling.All;
            Converters.Add(new Vector3Converter());
            Converters.Add(new Vector3IntConverter());
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
}
