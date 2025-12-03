using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using UnityEngine;

namespace BeaverBuddies.Events.Serialization
{
    public class EntitySerializer : EventParamSerializerBase<BaseComponent, string>
    {
        public override bool CanSerialize(object obj)
        {
            return obj is BaseComponent && ((BaseComponent)obj).TryGetComponent<EntityComponent>(out _);
        }

        public override string Serialize(BaseComponent data, IReplayContext context)
        {
            return data.GetComponent<EntityComponent>().EntityId.ToString();
        }

        public override BaseComponent Deserialize(string entityID, IReplayContext context)
        {
            if (!Guid.TryParse(entityID, out Guid guid))
            {
                // TODO: Think about logging
                //LogWarning($"Could not parse guid: {entityID}");
                return null;
            }
            var entity = context.GetSingleton<EntityRegistry>().GetEntity(guid);
            if (entity == null)
            {
                //LogWarning($"Could not find entity: {entityID}");
            }
            return entity;
        }
    }
}
