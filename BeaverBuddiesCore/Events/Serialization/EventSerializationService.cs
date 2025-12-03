using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeaverBuddies.Events.Serialization
{
    public class EventSerializationService
    {
        private readonly List<IEventParamSerializer> serializers = new List<IEventParamSerializer>();

        public EventSerializationService()
        {
            RegisterDefaultSerializers();
        }

        private void RegisterDefaultSerializers()
        {
            RegisterSerializer(new EntitySerializer());
        }

        public void RegisterSerializer(IEventParamSerializer serializer)
        {
            serializers.Add(serializer);
        }

        public IEventParamSerializer GetSerializerForInstance(object instance)
        {
            return serializers.FirstOrDefault(s => s.CanSerialize(instance));
        }
    }
}
