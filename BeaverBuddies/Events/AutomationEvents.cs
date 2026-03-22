using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.AutomationBuildingsUI;
using Timberborn.AutomationUI;
using Timberborn.BaseComponentSystem;
using Timberborn.FireworkSystem;

namespace BeaverBuddies.Events
{

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
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetContinuous)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetFireworkId)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetFlightDistance)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetHeading)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetPitch)),
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

        // It would be lovely to have this in a dict-like lookup, but unfortunately
        // since we can only get the type at runtime, generics won't work. So I could do
        // make everything take and return an object, but that's a pain, so I'll just do an if/else.
        // Right now, the if/else is easier, but if it becomes cumbersome, I can easily make this
        // into non-static methods and use an interface.
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
                    arguments[i] = GetEntityID((BaseComponent)arg);
                    if (arguments[i] == null)
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
                    arguments[i] = GetComponentForType(argType, (string)arg, context);
                }

                if (argType.IsEnum && (arg is long || arg is int))
                {
                    arguments[i] = Enum.ToObject(argType, arg);
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
    }

    public class SetAutomatableInputEvent : ReplayEvent
    {
        public string automatableID;
        public string inputID;

        public override void Replay(IReplayContext context)
        {
            Automatable automatable = GetComponent<Automatable>(context, automatableID);
            Automator automator = GetComponent<Automator>(context, inputID);
            if (automatable == null || automator == null) return;
            automatable.SetInput(automator);
        }

        public override string ToActionString()
        {
            return $"Setting Automatable {automatableID} input to {inputID}";
        }
    }

    [HarmonyPatch(typeof(AutomatableFragment), nameof(AutomatableFragment.SetInput))]
    static class AutomatableFragmentSetInputPatch
    {
        static bool Prefix(AutomatableFragment __instance, Automator automator)
        {
            return ReplayEvent.DoEntityPrefix(__instance._automatable, entityID =>
            {
                string automatorID = ReplayEvent.GetEntityID(automator);
                return new SetAutomatableInputEvent
                {
                    automatableID = entityID,
                    inputID = automatorID,
                };
            });
        }
    }

    public enum TimerIntervalInput
    {
        A,
        B
    }

    public class SetTimerIntervalEvent : ReplayEvent
    {
        public string entityID;
        public TimerIntervalInput input;
        public float time;
        public IntervalType intervalType;

        public override void Replay(IReplayContext context)
        {
            var timer = GetComponent<Timer>(context, entityID);
            if (timer == null) return;

            TimerInterval interval;
            if (input == TimerIntervalInput.A)
            {
                interval = timer.TimerIntervalA;
            }
            else
            {
                interval = timer.TimerIntervalB;
            }

            switch (intervalType)
            {
                case IntervalType.Ticks:
                    interval.SetTicks((int)Math.Round(time));
                    break;
                case IntervalType.Hours:
                    interval.SetHours(time);
                    break;
                case IntervalType.Days:
                    interval.SetDays(time);
                    break;
            }
        }
        public override string ToActionString()
        {
            return $"Setting Timer interval {input} to {intervalType}={time} for Entity {entityID}";
        }
    }

    // Because TimerIntervalElements don't have access to the Timer they're editing, we need to store it.
    [HarmonyPatch(typeof(TimerFragment), nameof(TimerFragment.ShowFragment))]
    static class TimerFragmentShowFragmentPatch
    {
        public static Timer CurrentEditingTimer { get; private set; }
        static void Prefix(BaseComponent entity)
        {
            CurrentEditingTimer = entity.GetComponent<Timer>();
        }
    }

    // There's no easy way to capture a TimerInterval edit, so we capture it manually.
    [HarmonyPatch(typeof(TimerIntervalElement), nameof(TimerIntervalElement.SetTimeInterval))]
    static class TimerIntervalElementSetTimeIntervalPatch
    {
        static bool Prefix(TimerIntervalElement __instance, float time, IntervalType intervalType)
        {
            Timer timer = TimerFragmentShowFragmentPatch.CurrentEditingTimer;
            if (timer == null) return true;
            TimerIntervalInput input = timer.TimerIntervalA == __instance._timerInterval ? TimerIntervalInput.A : TimerIntervalInput.B;
            return ReplayEvent.DoEntityPrefix(timer, entityID =>
            {
                return new SetTimerIntervalEvent
                {
                    entityID = entityID,
                    input = input,
                    time = time,
                    intervalType = intervalType,
                };
            });
        }
    }

    public class ResetTransmitterEvent : ReplayEvent
    {
        public string entityID;
        public bool resetAll;

        public override void Replay(IReplayContext context)
        {
            if (resetAll)
            {
                ISequentialTransmitter transmitter = GetComponent<ISequentialTransmitter>(context, entityID);
                if (transmitter == null) return;
                transmitter.Reset();
            }
            else
            {
                Automator automator = GetComponent<Automator>(context, entityID);
                if (automator == null) return;
                context.GetSingleton<AutomationResetter>().ResetPartition(automator);
            }
        }

        public override string ToActionString()
        {
            return $"Resetting {(resetAll ? "partition" : "transmitter")} for Entity {entityID}";
        }
    }

    [HarmonyPatch(typeof(SequentialTransmitterResetFragment), nameof(SequentialTransmitterResetFragment.OnReset))]
    static class SequentialTransmitterResetFragmentOnResetPatch
    {
        static bool Prefix(SequentialTransmitterResetFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._automator, entityID =>
            {
                return new ResetTransmitterEvent
                {
                    entityID = entityID,
                    resetAll = false,
                };
            });
        }
    }

    [HarmonyPatch(typeof(SequentialTransmitterResetFragment), nameof(SequentialTransmitterResetFragment.OnResetAll))]
    static class SequentialTransmitterResetFragmentOnResetAllPatch
    {
        static bool Prefix(SequentialTransmitterResetFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._automator, entityID =>
            {
                return new ResetTransmitterEvent
                {
                    entityID = entityID,
                    resetAll = true,
                };
            });
        }
    }
}
