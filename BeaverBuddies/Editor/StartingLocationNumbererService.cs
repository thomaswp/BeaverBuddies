using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;
using Timberborn.StartingLocationSystem;

namespace BeaverBuddies.Editor
{
    public class StartingLocationNumbererService : ILoadableSingleton
    {
        private readonly EntityComponentRegistry _entityComponentRegistry;

        private readonly EntityService _entityService;

        private readonly EventBus _eventBus;

        public StartingLocationNumbererService(EntityComponentRegistry entityComponentRegistry, EntityService entityService, EventBus eventBus)
        {
            _entityComponentRegistry = entityComponentRegistry;
            _entityService = entityService;
            _eventBus = eventBus;
        }

        public void Load()
        {
            _eventBus.Register(this);
        }

        [OnEvent]
        public void OnEntityInitializedEvent(EntityInitializedEvent entityInitializedEvent)
        {
            if (entityInitializedEvent.Entity.TryGetComponentFast<StartingLocation>(out var component))
            {
                Plugin.Log("String location placed!");
                StartingLocationPlayer startingLocationPlayer = entityInitializedEvent.Entity.GetComponentFast<StartingLocationPlayer>();
                if (startingLocationPlayer == null)
                {
                    Plugin.LogError("Cannot find StartingLocationPlayer");
                    return;
                }
                // TODO: Why are a bunch of starting locations creation?
                // E.g. is it that each block in the structure is a separate one?
                Plugin.Log(entityInitializedEvent.Entity.EntityId.ToString() + " -> #" + startingLocationPlayer.PlayerIndex);
            }
        }
    }
}
