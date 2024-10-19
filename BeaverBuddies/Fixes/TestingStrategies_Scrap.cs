using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WaterSystem;

/*
 * NOTE: This file is not actually compiled
 * It is a record of strategies for searching for fixes, such as slowly
 * activating Singltons or Tickables until a desync is created.
 */

# warning This file is not intended to be compiled!

namespace BeaverBuddies.Fixes
{

    // TODO: Remove - testing only
    [HarmonyPatch(typeof(TickableSingletonService), nameof(TickableSingletonService.StartParallelTick))]
    class TickableSingletonServiceStartParallelTickPatcher
    {
        static bool Prefix(TickableSingletonService __instance)
        {
            if (EventIO.IsNull) return true;
            ImmutableArray<IParallelTickableSingleton>.Enumerator enumerator =
                __instance._parallelTickableSingletons.GetEnumerator();
            while (enumerator.MoveNext())
            {
                // Run directly rather than using the thread pool
                IParallelTickableSingleton parallelTickable = enumerator.Current;
                //if (parallelTickable is SoilContaminationSimulationController) continue;
                //if (parallelTickable is WaterRenderer) continue;
                //if (parallelTickable is SoilMoistureSimulationController) continue;
                __instance._parallelizerContext.Run(delegate
                {
                    parallelTickable.ParallelTick();
                });
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(TickableSingletonService), nameof(TickableSingletonService.TickSingletons))]
    class TickableSingletonServiceTickSingletonsPatcher
    {
        static int tick = 0;

        static HashSet<Type> whitelist = new HashSet<Type>()
        {
            typeof (ThreadSafeWaterMap),
            typeof (WaterSourceRegistry),
            //typeof (TerrainMaterialMap),
            typeof (DayNightCycle),
        };

        static bool Prefix(TickableSingletonService __instance)
        {
            int max = tick / 3;
            max = __instance._tickableSingletons.Length;
            //max = 0;
            Plugin.Log($"TickSingletons through: {max}");
            if (max > 0)
            {
                var singleton = __instance._tickableSingletons[max - 1];
                Plugin.Log($"Last ticking singletong: {singleton._tickableSingleton.GetType().Name}");
            }



            for (int i = 0; i < __instance._tickableSingletons.Length; i++)
            {
                var singleton = __instance._tickableSingletons[i];
                if (whitelist.Contains(singleton._tickableSingleton.GetType()))
                {
                    //Plugin.Log("Whitelist");
                    singleton.Tick();
                }
            }

            for (int i = 0; i < max && i < __instance._tickableSingletons.Length; i++)
            {
                var singleton = __instance._tickableSingletons[i];
                if (whitelist.Contains(singleton._tickableSingleton.GetType()))
                {
                    continue;
                }
                __instance._tickableSingletons[i].Tick();
            }
            tick++;
            return false;
        }
    }

    [HarmonyPatch(typeof(TickableEntity), nameof(TickableEntity.TickTickableComponents))]
    class TickableEntityTickPatcher
    {
        static int lastTick = 0;
        static HashSet<Type> whitelist = new HashSet<Type>();
        static HashSet<Type> blacklist = new HashSet<Type>()
        {
            typeof (WaterSource),
        };

        public static bool Prefix(TickableEntity __instance)
        {
            int currentTick = GetSingleton<ReplayService>()?.TicksSinceLoad ?? 0;

            ImmutableArray<MeteredTickableComponent>.Enumerator enumerator = __instance._tickableComponents.GetEnumerator();
            while (enumerator.MoveNext())
            {
                MeteredTickableComponent current = enumerator.Current;
                if (current.Enabled)
                {
                    Type type = current._tickableComponent.GetType();
                    if (blacklist.Contains(type))
                    {
                        continue;
                    }
                    //if (!whitelist.Contains(type))
                    //{
                    //    if (currentTick > lastTick)
                    //    {
                    //        lastTick = currentTick;
                    //        whitelist.Add(type);
                    //        Plugin.Log($"Adding {type.Name}");
                    //    }
                    //    else
                    //    {
                    //        continue;
                    //    }
                    //}
                    current.StartAndTick();
                }
            }
            return false;
        }
    }
}
