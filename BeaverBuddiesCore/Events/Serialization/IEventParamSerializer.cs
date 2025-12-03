using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Events.Serialization
{
    public interface IEventParamSerializer
    {
        bool CanSerialize(object obj);
        object Serialize(object obj, IReplayContext context);
        object Deserialize(object data, IReplayContext context);
    }

    //protected interface IEventParamSerializer<InType, OutType> : IEventParamSerializer
    //{
    //    OutType Serialize(InType obj, IReplayContext context);
    //    InType Deserialize(OutType data, IReplayContext context);
    //}

    public abstract class EventParamSerializerBase<InType, OutType>
    {
        public abstract bool CanSerialize(object obj);
        public abstract OutType Serialize(InType obj, IReplayContext context);
        public abstract InType Deserialize(OutType data, IReplayContext context);
        public object Serialize(object obj, IReplayContext context)
        {
            return Serialize((InType)obj, context);
        }
        public object Deserialize(object data, IReplayContext context)
        {
            return Deserialize((OutType)data, context);
        }
    }
}
