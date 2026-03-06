using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;

namespace BeaverBuddies.Events
{
    public class AutomationEvent : ReplayEvent
    {
        public string entityID;
        public string methodKey;
        public object[] arguments;

        private static readonly Dictionary<string, MethodInfo> methodCache = new();

        private static string MakeKey(string className, string methodName) => $"{className}.{methodName}";

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
            object componentObj = GetComponentForType(methodInfo.DeclaringType, context);
            methodInfo.Invoke(componentObj, arguments);
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

        private object GetComponentForType(Type type, IReplayContext context)
        {
            var methodInfo = typeof(AutomationEvent).GetMethod(nameof(GetComponent),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (methodInfo == null || methodInfo.GetParameters().Length != 2)
            {
                throw new Exception($"GetComponent method not found or has incorrect parameters: {methodInfo?.GetParameters().Length}.");                
            }
            var genericMethod = methodInfo.MakeGenericMethod(type);
            return genericMethod.Invoke(this, new object[] { context, entityID });
        }

        public static void PatchAll(Harmony harmony)
        {
            (Type, string)[] methodsToPatchInfo = new (Type, string)[]
            {
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
            };
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

        private static bool DoPrefix(BaseComponent entity, string methodKey, object[] arguments)
        {
            return DoEntityPrefix(entity, entityID =>
            {
                return new AutomationEvent
                {
                    entityID = entityID,
                    methodKey = methodKey,
                    arguments = arguments
                };
            });
        }
    }
}
