using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Timberborn.Coordinates;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using System.Linq;
using Timberborn.Forestry;
using Timberborn.DropdownSystem;
using UnityEngine.UIElements;
using Timberborn.TimeSystem;
using Timberborn.Gathering;
using Timberborn.PrefabSystem;
using Timberborn.EntitySystem;
using Timberborn.Workshops;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;

namespace TimberModTest.Events
{
    public interface IReplayContext
    {
        T GetSingleton<T>();
    }

    public abstract class ReplayEvent : IComparable<ReplayEvent>
    {
        public float timeInFixedSecs;
        public int ticksSinceLoad;
        public int? randomS0Before;

        public string type => GetType().Name;

        public int CompareTo(ReplayEvent other)
        {
            if (other == null)
                return 1;
            //return timeInFixedSecs.CompareTo(other.timeInFixedSecs);
            return ticksSinceLoad.CompareTo(other.ticksSinceLoad);
        }

        public abstract void Replay(IReplayContext context);

        public override string ToString()
        {
            return type;
        }

        public virtual string ToActionString()
        {
            return $"Doing: {type}";
        }

        protected EntityComponent GetEntityComponent(IReplayContext context, string entityID)
        {
            if (!Guid.TryParse(entityID, out Guid guid))
            {
                Plugin.LogWarning($"Could not parse guid: {entityID}");
                return null;
            }
            var entity = context.GetSingleton<EntityRegistry>().GetEntity(guid);
            if (entity == null)
            {
                Plugin.LogWarning($"Could not find entity: {entityID}");
            }
            return entity;
        }

        protected T GetComponent<T>(IReplayContext context, string entityID) where T : BaseComponent
        {
            var entity = GetEntityComponent(context, entityID);
            if (entity == null) return null;
            var component = entity.GetComponentFast<T>();
            if (component == null)
            {
                Plugin.LogWarning($"Could not find component {typeof(T)} on entity {entityID}");
            }
            return component;
        }

        public static string GetEntityID(BaseComponent component)
        {
            return component?.GetComponentFast<EntityComponent>()?.EntityId.ToString();
        }

        protected Building GetBuilding(IReplayContext context, string buildingName)
        {
            var result = context.GetSingleton<BuildingService>().GetBuildingPrefab(buildingName);
            if (result == null)
            {
                Plugin.LogWarning($"Could not find building prefab: {buildingName}");
            }
            return result;
        }

        public static string GetBuildingName(BaseComponent component)
        {
            return component.GetComponentFast<Prefab>()?.PrefabName;
        }

        /// <summary>
        /// Helper method to make overriding recorded actions in game easier.
        /// </summary>
        /// <param name="getEvent">
        /// A function that returns the event to record, or null
        /// if we should skip recording and do the default method behavior.
        /// </param>
        /// <returns>True if the method should use default behavior</returns>
        public static bool DoPrefix(Func<ReplayEvent> getEvent)
        {
            // If we haven't loaded yet, just use default behavior
            // (e.g. during loading)
            if (!ReplayService.IsLoaded) return true;
            
            // Get the event and if it's null, just use default behavior
            ReplayEvent message = getEvent();
            if (message == null) return true;

            // Optional: Log the message
            Plugin.Log(message.ToActionString());

            // Record the event
            ReplayService.RecordEvent(message);

            // Return based on the EventIO's desired behavior
            return EventIO.ShouldPlayPatchedEvents;
        }

        public static bool DoEntityPrefix(BaseComponent component, Func<string, ReplayEvent> doRecord)
        {
            return DoPrefix(() =>
            {
                string entityID = GetEntityID(component);
                // If this is happening to a non-entity (e.g. prefab),
                // just let the base method handle it
                if (entityID == null) return null;
                return doRecord(entityID);
            });
        }
    }

    [Serializable]
    public class RandomStateSetEvent : ReplayEvent
    {
        public int seed;
        public int newTicksSinceLoad;
        public int entityUpdateHash;
        public int positionHash;

        public override void Replay(IReplayContext context)
        {
            UnityEngine.Random.InitState(seed);
            Plugin.Log($"Setting seed to {seed}; s0 = {UnityEngine.Random.state.s0}");

            if (context != null)
            {
                context.GetSingleton<ReplayService>().SetTicksSinceLoad(newTicksSinceLoad);
                TEBPatcher.SetHashes(entityUpdateHash, positionHash);
            }
        }

        public static RandomStateSetEvent CreateAndExecute(int ticksSinceLoad)
        {
            int seed = UnityEngine.Random.RandomRangeInt(int.MinValue, int.MaxValue);
            RandomStateSetEvent message = new RandomStateSetEvent()
            {
                seed = seed,
                newTicksSinceLoad = ticksSinceLoad,
                entityUpdateHash = TEBPatcher.EntityUpdateHash,
                positionHash = TEBPatcher.PositionHash,
            };
            // TODO: Not certain if this is the right time, or if it should be enqueued
            message.Replay(null);
            return message;
        }
    }

    [Serializable]
    public class SpeedSetEvent : ReplayEvent
    {
        public float speed;

        public override void Replay(IReplayContext context)
        {
            SpeedManager sm = context.GetSingleton<SpeedManager>();
            Plugin.Log($"Event: Changing speed from {sm.CurrentSpeed} to {speed}");
            if (sm.CurrentSpeed != speed) sm.ChangeSpeed(speed);

            ReplayService replayService = context.GetSingleton<ReplayService>();
            if (speed != replayService.TargetSpeed)
            {
                Plugin.Log($"Event: Changing target speed from {replayService.TargetSpeed} to {speed}");
                replayService.SetTargetSpeed(speed);
            }
        }
    }

    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.ChangeSpeed))]
    public class SpeedChangePatcher
    {
        private static bool silently = false;

        public static void SetSpeedSilently(SpeedManager speedManager, float speed)
        {
            silently = true;
            speedManager.ChangeSpeed(speed);
            silently = false;
        }

        static bool Prefix(SpeedManager __instance, ref float speed)
        {
            // No need to log speed changes to current speed
            if (__instance.CurrentSpeed == speed) return true;
            // Also don't log if we're silent
            if (silently) return true;

            ReplayService.RecordEvent(new SpeedSetEvent()
            {
                speed = speed
            });

            if (EventIO.ShouldPlayPatchedEvents)
            {
                // If this will actually change the speed, make sure
                // we shouldn't pause instead.
                if (EventIO.ShouldPauseTicking) speed = 0;
                return true;
            }
            return false;
        }
    }

    // TODO: Overwrite SpeedManager.LockSpeed and see where it's
    // being called. My guess is we don't ever want to lock speed
    // if we're the client, and probably only on pause if we're the
    // server. We may also want to disable certain things if client
    // e.g. how would menu pause be handled? Save would be unstable.
    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.LockSpeed))]
    public class SpeedLockPatcher
    {
        static bool Prefix(SpeedManager __instance)
        {
            Plugin.Log("SpeedManager.LockSpeed");
            Plugin.LogStackTrace();
            return true;
        }
    }

}
