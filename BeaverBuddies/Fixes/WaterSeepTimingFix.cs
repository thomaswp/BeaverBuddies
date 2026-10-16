using System.Runtime.CompilerServices;
using BeaverBuddies.IO;
using HarmonyLib;
using Timberborn.TickSystem;
using Timberborn.WaterSourceSystem;
using UnityEngine;

namespace BeaverBuddies.Fixes
{
    // The vanilla getter advances source strength using render-frame deltaTime.
    // Advance only during the mod's deterministic late source tick instead.
    [HarmonyPatch(typeof(WaterDepthStrengthModifier), nameof(WaterDepthStrengthModifier.GetStrengthModifier))]
    internal static class WaterSeepTimingFix
    {
        private sealed class StepState
        {
            internal ReplayService Session;
            internal int Tick = -1;
        }
        private static readonly ConditionalWeakTable<WaterDepthStrengthModifier, StepState> steps =
            new ConditionalWeakTable<WaterDepthStrengthModifier, StepState>();

        internal static bool Prefix(WaterDepthStrengthModifier __instance, ref float __result)
        {
            if (EventIO.IsNull) return true;
            var replay = SingletonManager.GetSingleton<ReplayService>();
            // Loading/registration and incidental getter calls must not advance time
            // or mutate the saved modifier based on partially initialized water data.
            if (ReplayService.IsLoaded && replay != null && LateTickableBuffer.TickingLate)
            {
                var state = steps.GetOrCreateValue(__instance);
                int tick = replay.TicksSinceLoad;
                if (!ReferenceEquals(state.Session, replay) || state.Tick != tick)
                {
                    float elapsed = replay.GetSingleton<TickService>().TickIntervalInSeconds;
                    float before = __instance._currentModifier;
                    __instance.UpdateEnabledState();
                    __instance._currentModifier = __instance._isEnabled
                        ? Mathf.MoveTowards(before, 1f, WaterDepthStrengthModifier.FadeInSpeed * elapsed)
                        : 0f;
                    state.Session = replay;
                    state.Tick = tick;
                }
            }
            __result = __instance._currentModifier;
            return false;
        }
    }
}
