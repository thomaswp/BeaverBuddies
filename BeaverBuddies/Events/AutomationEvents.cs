using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;

namespace BeaverBuddies.Events
{

    // It would be lovely to have this in a dict-like lookup, but unfortunately
    // since we can only get the type at runtime, generics won't work. So I could do
    // casting here or I could just have a big if/else.
    // Right now, the if/else is easier, but if it becomes cumbersome, I can easily make this
    // into non-static methods and use an interface.
    class ComponentSerializer
    {
        // This doesn't actually work because it's generic!
        public static bool TryDeserialize<T>(string data, IReplayContext context, out T result) where T: BaseComponent
        {
            result = ReplayEvent.GetComponent<T>(context, data);
            return result != null;
        }

        public static bool TrySerialize(BaseComponent obj, out string result)
        {
            result = ReplayEvent.GetEntityID(obj);
            return result != null;
        }
    }

    public class AutomationEvent : ReplayEvent
    {
        public string entityID;
        public string methodKey;
        public object[] arguments;

        private static readonly Dictionary<string, MethodInfo> methodCache = new();

        private static string MakeKey(string className, string methodName) => $"{className}.{methodName}";

        private static MethodInfo getComponentMethodInfo;

        public override void Replay(IReplayContext context)
        {
            if (!TryGetMethodInfo(methodKey, out var methodInfo))
            {
                Plugin.LogError($"No MethodInfo for: {methodKey}. Cannot replay this event.");
                return;
            }
            if (methodInfo.GetParameters().Length != arguments.Length)
            {
                Plugin.LogError($"Argument count mismatch for {methodKey}. Expected {methodInfo.GetParameters().Length}, got {arguments.Length}. Cannot replay this event.");
                return;
            }
            object componentObj = GetComponentForType(methodInfo.DeclaringType, entityID, context);
            object[] deserialized = Deserialize(arguments, context, methodInfo);
            methodInfo.Invoke(componentObj, deserialized);
        }

        public override string ToActionString()
        {
            return $"Calling {methodKey} for Entity {entityID}";
        }

        static bool TryGetMethodInfo(string methodKey, out MethodInfo info)
        {
            if (methodCache.TryGetValue(methodKey, out var cachedInfo))
            {
                info = cachedInfo;
                return true;
            }
            info = default;
            return false;
        }

        private static MethodInfo GetMethodInfoForGetComponent()
        {
            if (getComponentMethodInfo != null)
            {
                return getComponentMethodInfo;
            }
            var methodInfo = typeof(ReplayEvent).GetMethod(nameof(GetComponent),
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (methodInfo == null || methodInfo.GetParameters().Length != 2)
            {
                throw new Exception($"GetComponent method not found or has incorrect parameters: {methodInfo?.GetParameters().Length}.");
            }
            return methodInfo;
        }

        private static object GetComponentForType(Type type, string entityID, IReplayContext context)
        {
            MethodInfo methodInfo = GetMethodInfoForGetComponent();
            var genericMethod = methodInfo.MakeGenericMethod(type);
            return genericMethod.Invoke(null, new object[] { context, entityID });
        }

        // TODO: Test building duplication (e.g. Indicator)
        public static void PatchAll(Harmony harmony)
        {
            // Important: We can only override methods like this if they:
            // 1) Are only ever called from the UI (not from step logic)
            // 2) Are in a BaseComponent (so we can get the EntityID)
            (Type, string)[] methodsToPatchInfo =
            [
                (typeof(Chronometer), nameof(Chronometer.SetStartTime)),
                (typeof(Chronometer), nameof(Chronometer.SetEndTime)),
                (typeof(Chronometer), nameof(Chronometer.SetMode)),
                (typeof(ContaminationSensor), nameof(ContaminationSensor.SetMode)),
                (typeof(ContaminationSensor), nameof(ContaminationSensor.SetThreshold)),
                (typeof(DepthSensor), nameof(DepthSensor.SetMode)),
                (typeof(DepthSensor), nameof(DepthSensor.SetThreshold)),
                (typeof(FlowSensor), nameof(FlowSensor.SetMode)),
                (typeof(FlowSensor), nameof(FlowSensor.SetThreshold)),
                (typeof(Gate), nameof(Gate.SetOpeningMode)),
                (typeof(Indicator), nameof(Indicator.SetColorReplicationEnabled)),
                (typeof(Indicator), nameof(Indicator.SetJournalEntryEnabled)),
                (typeof(Indicator), nameof(Indicator.SetPinnedMode)),
                (typeof(Indicator), nameof(Indicator.SetWarningEnabled)),
                // Note: Lever calls these methods during load, but this should be
                // ok because we don't record events until the game has started
                (typeof(Lever), nameof(Lever.SetPinned)),
                (typeof(Lever), nameof(Lever.SetSpringReturn)),
                // Note: This won't be a great UX, since we really have to make
                // this an event, which will make the UI laggy, but I think that's
                // unavoidable.
                (typeof(Lever), nameof(Lever.SwitchState)),
                (typeof(Memory), nameof(Memory.SetMode)),
                (typeof(Memory), nameof(Memory.SetInputA)),
                (typeof(Memory), nameof(Memory.SetInputB)),
                (typeof(Memory), nameof(Memory.SetResetInput)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetComparisonMode)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetCountBeavers)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetCountBots)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetGlobalMode)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetMode)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetThreshold)),
                (typeof(PowerMeter), nameof(PowerMeter.SetComparisonMode)),
                (typeof(PowerMeter), nameof(PowerMeter.SetIntThreshold)),
                (typeof(PowerMeter), nameof(PowerMeter.SetMode)),
                (typeof(PowerMeter), nameof(PowerMeter.SetPercentThreshold)),
                (typeof(Relay), nameof(Relay.SetInputA)),
                (typeof(Relay), nameof(Relay.SetInputB)),
                (typeof(Relay), nameof(Relay.SetMode)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetComparisonMode)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetFillRateThreshold)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetGoodId)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetIncludeInputs)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetMode)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetThreshold)),
                (typeof(ScienceCounter), nameof(ScienceCounter.SetMode)),
                (typeof(ScienceCounter), nameof(ScienceCounter.SetThreshold)),
                (typeof(Speaker), nameof(Speaker.SetPlaybackMode)),
                (typeof(Speaker), nameof(Speaker.SetSoundId)),
                (typeof(Speaker), nameof(Speaker.SetSpatialMode)),
                // Note: Timer calls these methods during load (see above)
                (typeof(Timer), nameof(Timer.SetInput)),
                (typeof(Timer), nameof(Timer.SetMode)),
                (typeof(Timer), nameof(Timer.SetResetInput)),
                (typeof(WeatherStation), nameof(WeatherStation.SetEarlyActivationEnabled)),
                (typeof(WeatherStation), nameof(WeatherStation.SetEarlyActivationHours)),
                (typeof(WeatherStation), nameof(WeatherStation.SetMode)),
                // Note: this intentionally omits the HTTPApi system because, well, that
                // doesn't really make sense in multiplayer... at the very least it'd be
                // a larger project.
            ];
            var methodsToPatch = methodsToPatchInfo.Select(
                info => info.Item1.GetMethod(
                    info.Item2, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ));
            foreach (var method in methodsToPatch)
            {
                OverrideMethod(harmony, method);
            }
        }

        private static void OverrideMethod(Harmony harmony, MethodInfo info)
        {
            string key = MakeKey(info.DeclaringType.FullName, info.Name);
            methodCache[key] = info;

            // Prepare the Harmony Prefix
            // We point Harmony to our UniversalPrefix method below
            var prefix = typeof(AutomationEvent).GetMethod(nameof(UniversalPrefix), BindingFlags.NonPublic | BindingFlags.Static);

            // Apply the patch
            harmony.Patch(info, prefix: new HarmonyMethod(prefix));
        }

        // This is the method Harmony actually calls
        private static bool UniversalPrefix(BaseComponent __instance, MethodBase __originalMethod, object[] __args)
        {
            // Use the same key logic to identify which method was triggered
            string key = MakeKey(__originalMethod.DeclaringType.FullName, __originalMethod.Name);

            // Call the original logic to create and record the event
            return DoPrefix(__instance, key, __args);
        }

        private static object[] Serialize(object[] arguments)
        {
            object[] argsCopy = new object[arguments.Length];
            Array.Copy(arguments, argsCopy, arguments.Length);
            arguments = argsCopy;
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                if (arg == null)
                    continue;
                if (arg is BaseComponent)
                {
                    if (ComponentSerializer.TrySerialize((BaseComponent)arg, out var serialized))
                    {
                        arguments[i] = serialized;
                    }
                    else
                    {
                        Plugin.LogWarning($"Failed to serialize argument of type {arg.GetType().Name} with value {arg}. This may cause issues during replay.");
                    }
                }
            }
            return arguments;
        }

        private object[] Deserialize(object[] arguments, IReplayContext context, MethodInfo methodInfo)
        {
            object[] argsCopy = new object[arguments.Length];
            Array.Copy(arguments, argsCopy, arguments.Length);
            arguments = argsCopy;
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                if (arg == null)
                    continue;
                if (i >= methodInfo.GetParameters().Length)
                {
                    Plugin.LogError($"Argument index {i} is out of range for method {methodInfo.Name}. Cannot deserialize this argument.");
                    continue;
                }
                Type argType = methodInfo.GetParameters()[i].ParameterType;

                if (typeof(BaseComponent).IsAssignableFrom(argType) && arg is string)
                {
                    var result = GetComponentForType(argType, (string)arg, context);
                    if (result != null)
                    {
                        arguments[i] = result;
                    }
                    else
                    {
                        Plugin.LogWarning($"Failed to get component of type {argType.Name} with ID {(string)arg}. This may cause issues during replay.");
                    }
                }
            }
            return arguments;
        }

        private static bool DoPrefix(BaseComponent entity, string methodKey, object[] arguments)
        {
            return DoEntityPrefix(entity, entityID =>
            {
                //Plugin.Log($"Serializing args for: {methodKey}: {arguments.Length}");
                //Plugin.LogStackTrace();
                return new AutomationEvent
                {
                    entityID = entityID,
                    methodKey = methodKey,
                    arguments = Serialize(arguments)
                };
            });
        }

        /*
         * TODO: Implement manually:
         * Lever:
         *  SetSpringReturn
         *  SetPinned
         *  SwitchState
         */
    }

    [HarmonyPatch(typeof(Lever), nameof(Lever.Load))]
    static class LeverLoadPatch
    {
        static void Prefix()
        {
            
        }

        static void Postfix()
        {

        }
    }
}
