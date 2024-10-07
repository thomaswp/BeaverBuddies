using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.TickSystem;
using Timberborn.WaterSourceSystem;

namespace BeaverBuddies.Fixes
{
    internal struct WaterSourceChange
    {
        public WaterSource source;
        public float newStrength;
    }

    public class LateTickableBuffer : RegisteredSingleton
    {
        private List<TickableComponent> buffer = new List<TickableComponent>();

        public static bool TickingLate { get; private set; } = false;

        internal void Add(WaterSource change)
        {
            buffer.Add(change);
        }

        public void TickComponents()
        {
            TickingLate = true;
            foreach (var change in buffer)
            {
                change.Tick();
            }
            TickingLate = false;
            buffer.Clear();
        }

    }

    [HarmonyPatch(typeof(WaterSource), nameof(WaterSource.Tick))]
    class WaterSourceTickPatcher
    {
        static bool Prefix(WaterSource __instance)
        {
            if (LateTickableBuffer.TickingLate)
            {
                return true;
            }
            var buffer = SingletonManager.GetSingleton<LateTickableBuffer>();
            if (buffer == null)
            {
                return true;
            }
            buffer.Add(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(TickableSingletonService), nameof(TickableSingletonService.FinishParallelTick))]
    class TickableSingletonServiceFinishParallelTickPatcher
    {
        public void Postfix()
        {
            // Once the simulation has finished, enact the changes to water sources
            var buffer = SingletonManager.GetSingleton<LateTickableBuffer>();
            buffer?.TickComponents();
        }
    }
}
