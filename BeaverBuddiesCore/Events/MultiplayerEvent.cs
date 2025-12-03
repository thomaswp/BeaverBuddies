using System;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.BaseComponentSystem;
using Timberborn.TemplateSystem;
using Timberborn.BlueprintSystem;

namespace BeaverBuddies.Events
{

    public abstract class MultiplayerEvent : IComparable<MultiplayerEvent>
    {
        /// <summary>
        /// Captures the number of ticks since the game was loaded when this event was created.
        /// Do not modify this value directly; it is set when the event is created.
        /// </summary>
        public int ticksSinceLoad;

        /// <summary>
        /// Captures the random state before the event was created, if applicable.
        /// Do not modify this value directly; it is set when the event is created.
        /// </summary>
        public int? randomS0Before;

        // LogWarning action can be set by the user of this class to handle warnings
        protected static Action<string> LogWarning { get; set; } = s => { };

        public virtual string type => GetType().Name;

        public int CompareTo(MultiplayerEvent other)
        {
            if (other == null)
                return 1;
            //return timeInFixedSecs.CompareTo(other.timeInFixedSecs);
            return ticksSinceLoad.CompareTo(other.ticksSinceLoad);
        }

        /// <summary>
        /// Replays this event using its parameters.
        /// RelplayEvents should override this method to implement their functionality.
        /// </summary>
        /// <param name="context">A context that can be used to fetch relevant Singletons</param>
        public abstract void Replay(IReplayContext context);

        public override string ToString()
        {
            return type;
        }

        /// <summary>
        /// A human-readable description of the action being performed by this event.
        /// This is used only for logging and debugging purposes.
        /// For example "Constructing {buildingName} at {x},{y}".
        /// ReplayEvents should override this method to provide more detailed descriptions.
        /// </summary>
        /// <returns></returns>
        public virtual string ToActionString()
        {
            return $"Doing: {type}";
        }

        /// <summary>
        /// Helper function that gets an EntityComponent from an entity ID string, if it exists.
        /// Otherwise, logs a warning and returns null.
        /// </summary>
        /// <param name="context">The IReplayContext for this event.</param>
        /// <param name="entityID">The entity ID string.</param>
        /// <returns></returns>
        protected EntityComponent GetEntityComponent(IReplayContext context, string entityID)
        {
            if (!Guid.TryParse(entityID, out Guid guid))
            {
                LogWarning($"Could not parse guid: {entityID}");
                return null;
            }
            var entity = context.GetSingleton<EntityRegistry>().GetEntity(guid);
            if (entity == null)
            {
                LogWarning($"Could not find entity: {entityID}");
            }
            return entity;
        }

        /// <summary>
        /// Helper function that gets the specified component from the entity with the given ID.
        /// If the entity or component does not exist, logs a warning and returns default(T).
        /// </summary>
        /// <typeparam name="T">The type of the component to fetch.</typeparam>
        /// <param name="context">The IReplayContext for this event.</param>
        /// <param name="entityID">The entity ID string.</param>
        /// <returns></returns>
        protected T GetComponent<T>(IReplayContext context, string entityID)
        {
            var entity = GetEntityComponent(context, entityID);
            if (entity == null) return default;
            var component = entity.GetComponent<T>();
            if (component == null)
            {
                LogWarning($"Could not find component {typeof(T)} on entity {entityID}");
            }
            return component;
        }

        /// <summary>
        /// Helper function that gets the entity ID from a component, if it exists.
        /// </summary>
        /// <param name="component">The component to get the entity ID from.</param>
        /// <returns>The entity ID string, or null if it could not be found.</returns>
        public static string GetEntityID(BaseComponent component)
        {
            return component?.GetComponent<EntityComponent>()?.EntityId.ToString();
        }
    }

}
