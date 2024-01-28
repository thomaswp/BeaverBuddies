using System;
using HarmonyLib;
using Timberborn.Buildings;
using Timberborn.TimeSystem;
using Timberborn.PrefabSystem;
using Timberborn.EntitySystem;
using Timberborn.BaseComponentSystem;
using static TimberModTest.SingletonManager;
using Timberborn.OptionsGame;
using Timberborn.Options;

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
            GetSingleton<ReplayService>().RecordEvent(message);

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

            S<ReplayService>().RecordEvent(new SpeedSetEvent()
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

    // TODO: It might be nice to make this configurable: whether the host
    // should freeze the game for these pop-ups. If we don't freeze, it could
    // definitely cause some possible invalid operations (e.g. deleting a building
    // that's not there anymore), but in theory these errors get caught before
    // sending to the server. In practice, though, there could be side-effects of
    // and aborted event. For clients, I think this is always a possibility, regardless
    // of whether we freeze, since it's always happening at a delay.
    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.LockSpeed))]
    public class SpeedLockPatcher
    {
        static bool Prefix(SpeedManager __instance)
        {
            // Clients should never freeze for dialogs. Main menu will be
            // handled separately.
            if (EventIO.Get()?.UserEventBehavior == UserEventBehavior.Send)
            {
                return false;
            }

            if (!__instance._isLocked)
            {
                __instance._speedBefore = __instance.CurrentSpeed;
                SpeedChangePatcher.SetSpeedSilently(__instance, 0f);
                __instance._isLocked = true;
                __instance._eventBus.Post(new SpeedLockChangedEvent(__instance._isLocked));
            }
            return false;
        }
    }

    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.UnlockSpeed))]
    public class SpeedUnlockPatcher
    {
        static bool Prefix(SpeedManager __instance)
        {
            // Clients should never unfreeze for dialogs. See above.
            if (EventIO.Get()?.UserEventBehavior == UserEventBehavior.Send)
            {
                return false;
            }

            if (__instance._isLocked)
            {
                __instance._isLocked = false;
                SpeedChangePatcher.SetSpeedSilently(__instance, __instance._speedBefore);
                __instance._eventBus.Post(new SpeedLockChangedEvent(__instance._isLocked));
            }
            return false;
        }
    }

    [Serializable]
    class ShowOptionsMenuEvent : SpeedSetEvent
    {
        public ShowOptionsMenuEvent()
        {
            speed = 0;
        }

        public override void Replay(IReplayContext context)
        {
            base.Replay(context);
            context.GetSingleton<IOptionsBox>().Show();
        }
    }

    // We make showing the options menu a synced game event, rather than
    // a non-synced UI action, for two reasons:
    // 1) This ensures that the Options menu is always shown when a full
    //    tick has been completed.
    // 2) This will give other plays a visual clue about why the game has
    //    paused.
    // However, only the host will be able to unpause, and only by manually
    // setting the game speed, since they won't process any events by clients
    // while they have a panel (including this one) up (I think...).
    [HarmonyPatch(typeof(GameOptionsBox), nameof(GameOptionsBox.Show))]
    public class GameOptionsBoxShowPatcher
    {
        static bool Prefix()
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new ShowOptionsMenuEvent();
            });
        }
    }

}
