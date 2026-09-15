using HarmonyLib;
using BeaverBuddies.IO;
using Timberborn.TickSystem;
using Timberborn.SingletonSystem;
using Timberborn.WaterSourceSystem;
using UnityEngine;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Timberborn's WaterDepthStrengthModifier fades a water source's strength
    /// back in (after the water above it drops below its depth limit) using
    /// Time.deltaTime, i.e. the real duration of the last *frame*. It is called
    /// once per tick from WaterSource.Tick, so the fade advances by a
    /// machine-dependent amount each tick. WaterSourceRegistry snapshots the
    /// resulting CurrentStrength and the parallel water simulation adds that
    /// much water, so the water map diverges one tick later.
    ///
    /// Symptom in a desync trace: "Updating water map columns with hash" differs
    /// while column counts and moisture still match, usually within a few ticks
    /// of loading a save whose reservoir sits near a spring's depth limit.
    ///
    /// Fix: advance the fade by the tick interval instead of the frame time, so
    /// every machine computes the same strength for the same tick. This also
    /// matches the intent of FadeInSpeed (0.5 per second of game time).
    /// </summary>
    // ILoadableSingleton guarantees the game instantiates this service at scene
    // load even though nothing else depends on it (SingletonLifecycleService
    // collects singletons by lifecycle interface).
    public class WaterSourceStrengthFixService : RegisteredSingleton, ILoadableSingleton
    {
        private readonly ITickService _tickService;

        public float TickIntervalInSeconds { get; private set; }

        public WaterSourceStrengthFixService(ITickService tickService)
        {
            _tickService = tickService;
        }

        public void Load()
        {
            // TickService reads its spec in Load(); Timberborn orders Load() calls
            // by dependency, so the interval is available here.
            TickIntervalInSeconds = _tickService.TickIntervalInSeconds;
        }
    }

    [ManualMethodOverwrite]
    /*
        09/14/2026
        public float GetStrengthModifier()
        {
            UpdateEnabledState();
            _currentModifier = (_isEnabled ? Mathf.MoveTowards(_currentModifier, 1f, FadeInSpeed * Time.deltaTime) : 0f);
            return _currentModifier;
        }
     */
    [HarmonyPatch(typeof(WaterDepthStrengthModifier), nameof(WaterDepthStrengthModifier.GetStrengthModifier))]
    class WaterDepthStrengthModifierGetStrengthModifierPatcher
    {
        static bool Prefix(WaterDepthStrengthModifier __instance, ref float __result)
        {
            if (EventIO.IsNull) return true;
            var service = SingletonManager.GetSingleton<WaterSourceStrengthFixService>();
            if (service == null) return true;

            __instance.UpdateEnabledState();
            // Tick-based instead of frame-based (the only change from the original).
            float maxDelta = WaterDepthStrengthModifier.FadeInSpeed * service.TickIntervalInSeconds;
            __instance._currentModifier = __instance._isEnabled
                ? Mathf.MoveTowards(__instance._currentModifier, 1f, maxDelta)
                : 0f;
            __result = __instance._currentModifier;
            return false;
        }
    }
}
