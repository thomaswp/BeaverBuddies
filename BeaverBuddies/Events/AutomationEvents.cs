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
        public object argument;

        private static readonly Dictionary<string, MethodInfo> setterMethodCache = new();

        private static string MakeKey(string className, string methodName) => $"{className}.{methodName}";

        public override void Replay(IReplayContext context)
        {
            if (!TryGetSetterMethodInfo(methodKey, out var setterInfo))
            {
                Plugin.LogError($"No MethodInfo for: {methodKey}. Cannot replay this event.");
                return;
            }
            object componentObj = GetComponentForType(setterInfo.DeclaringType, context);
            setterInfo.Invoke(componentObj, new object[] { argument });
        }

        static bool TryGetSetterMethodInfo(string methodKey, out MethodInfo info)
        {
            if (setterMethodCache.TryGetValue(methodKey, out var cachedInfo))
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
            };
            var methodsToPatch = methodsToPatchInfo.Select(
                info => info.Item1.GetMethod(
                    info.Item2, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ));
            foreach (var method in methodsToPatch)
            {
                OverrideSetter(harmony, method);
            }
        }

        private static void OverrideSetter(Harmony harmony, MethodInfo info)
        {
            // Note that "setter" here is not a C# property setter, but rather
            // any method that takes a single argument.

            string key = MakeKey(info.DeclaringType.FullName, info.Name);
            setterMethodCache[key] = info;

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

            // Since these are "setters," we assume the first argument [0] is the value.
            // Harmony conveniently wraps this in an object array for us.
            object argument = (__args != null && __args.Length > 0) ? __args[0] : null;

            // Call your existing logic
            return DoPrefix(__instance, key, argument);
        }

        private static bool DoPrefix(BaseComponent entity, string methodKey, object argument)
        {
            return DoEntityPrefix(entity, entityID =>
            {
                return new AutomationEvent
                {
                    entityID = entityID,
                    methodKey = methodKey,
                    argument = argument
                };
            });
        }
    }
}
