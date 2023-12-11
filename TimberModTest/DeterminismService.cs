using HarmonyLib;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameScene;
using Timberborn.InputSystem;
using Timberborn.NaturalResources;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.NaturalResourcesReproduction;
using Timberborn.NeedSystem;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;
using Timberborn.SoundSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using UnityEngine;

namespace TimberModTest
{

    public static class DeterminismController
    {
        private static HashSet<System.Type> activeNonGamePatchers = new HashSet<System.Type>();
        public static bool IsTicking = false;
        public static Thread UnityThread;

        private static readonly List<StackTrace> lastRandomStackTraces = new List<StackTrace>();

        public static void ClearRandomStacks()
        {
            lastRandomStackTraces.Clear();
        }

        public static void PrintRandomStacks()
        {
            foreach (StackTrace stack in lastRandomStackTraces)
            {
                Plugin.Log(stack.ToString());
            }
        }

        public static bool ShouldFreezeSeed 
        {
            get
            {
                // Calls from a non-game thread should never use the game's random
                // though if they are game-related we may need a solution for that...
                if (UnityThread != null && Thread.CurrentThread != UnityThread)
                {
                    LogUnknownRandomCalled();
                    return true;
                }

                bool areActiveNonGamePatchers = activeNonGamePatchers.Count > 0;

                // If this is non-game code, don't use the Game's random
                if (areActiveNonGamePatchers) return true;

                // If we're ticking, it's likely game code, and hopefully
                // we've caught any non-game code that can run during a tick!
                if (IsTicking)
                {
                    // TODO: Make only in "dev mode"
                    lastRandomStackTraces.Add(new StackTrace());
                    return false;
                }

                // If we are replaying/playing events recorded from this
                // user or other clients, we should always use the game's random.
                if (ReplayService.IsReplayingEvents) return false;

                // If we're not ticking/replaying, and random is happening from an
                // unknown source, log it so we can classify it.
                LogUnknownRandomCalled();

                // And ultimately return true, assuming it's non-game code,
                // though we can't be sure and need to investigate.
                return true;
            }
        }

        public static bool SetNonGamePatcherActive(System.Type patcherType, bool active)
        {
            if (active)
            {
                return activeNonGamePatchers.Add(patcherType);
            }
            else
            {
                return activeNonGamePatchers.Remove(patcherType);
            }
        }

        //public static bool SetNonTickGamePatcherActive(System.Type patcherType, bool active)
        //{
        //    if (active)
        //    {
        //        return activeNonTickGamePatchers.Add(patcherType);
        //    }
        //    else
        //    {
        //        return activeNonTickGamePatchers.Remove(patcherType);
        //    }
        //}
        
        private static System.Random random = new System.Random();

        public static float Range(float inclusiveMin, float inclusiveMax)
        {
            return (float)random.NextDouble() * (inclusiveMax - inclusiveMin) + inclusiveMin;
        }

        public static int Range(int inclusiveMin, int exclusiveMax)
        {
            return random.Next(inclusiveMin, exclusiveMax);
        }

        public static Vector2 InsideUnitCircle()
        {
            var state = Random.state;
            var value = Random.insideUnitCircle;
            Random.state = state;
            return value;
        }

        private static void LogUnknownRandomCalled()
        {
            if (!ReplayService.IsLoaded) return;
            
            Plugin.LogWarning("Unknown random called outside of tick");
            Plugin.LogStackTrace();
        }
    }

    [HarmonyPatch(typeof(RandomNumberGenerator), nameof(RandomNumberGenerator.Range), typeof(float), typeof(float))]
    public class RandomRangeFloatPatcher
    {
        static bool Prefix(float inclusiveMin, float inclusiveMax, ref float __result)
        {
            if (DeterminismController.ShouldFreezeSeed)
            {
                __result = DeterminismController.Range(inclusiveMin, inclusiveMax);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(RandomNumberGenerator), nameof(RandomNumberGenerator.Range), typeof(int), typeof(int))]
    public class RandomRangeIntPatcher
    {
        static bool Prefix(int inclusiveMin, int exclusiveMax, ref int __result)
        {
            if (DeterminismController.ShouldFreezeSeed)
            {
                __result = DeterminismController.Range(inclusiveMin, exclusiveMax);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(RandomNumberGenerator), nameof(RandomNumberGenerator.InsideUnitCircle))]
    public class RandomUnitCirclePatcher
    {
        static bool Prefix(ref Vector2 __result)
        {
            if (DeterminismController.ShouldFreezeSeed)
            {
                __result = DeterminismController.InsideUnitCircle();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(InputService), nameof(InputService.Update))]
    public class InputPatcher
    {
        //private static readonly Random random = new Random();

        //private static Random.State state;

        // Just as a test, muck random sounds!
        static void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(InputPatcher), true);
        }

        static void Postfix()
        {
            //Random.state = state;
            DeterminismController.SetNonGamePatcherActive(typeof(InputPatcher), false);
        }
    }

    [HarmonyPatch(typeof(Sounds), nameof(Sounds.GetRandomSound))]
    public class SoundsPatcher
    {
        static void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(SoundsPatcher), true);
        }

        static void Postfix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(SoundsPatcher), false);
        }
    }

    [HarmonyPatch(typeof(SoundEmitter), nameof(SoundEmitter.Update))]
    public class SoundEmitterPatcher
    {
        static void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(SoundEmitter), true);
        }

        static void Postfix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(SoundEmitter), false);
        }
    }

    [HarmonyPatch(typeof(DateSalter), nameof(DateSalter.GenerateRandomNumber))]
    public class DateSalterPatcher
    {
        static void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(DateSalterPatcher), true);
        }

        static void Postfix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(DateSalterPatcher), false);
        }
    }

    [HarmonyPatch(typeof(PlantableDescriber), nameof(PlantableDescriber.GetPreviewFromPrefab))]
    public class PlantableDescriberPatcher
    {
        static void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(PlantableDescriberPatcher), true);
        }

        static void Postfix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(PlantableDescriberPatcher), false);
        }
    }


    [HarmonyPatch(typeof(Ticker), nameof(Ticker.Update))]
    public class TickerPatcher
    {
        static void Prefix()
        {
            DeterminismController.IsTicking = true;
        }
        static void Postfix()
        {
            DeterminismController.IsTicking = false;
        }
    }

    //TODO: Need to confirm that entity IDs are consistent and get added
    // to a consistent bucket
    // (based on possibly random or system-dependednt hash codes)
    //TickableBucketService.GetEntityBucketIndex determines this
    //By default, 128 buckets

    // TODO: Possibly related: when sleep occurs, the game seems
    // to desync. Not sure what that would happen, since it's just part
    // of the update loop, and I need to do a more definitive test...

    /*
     * Definite issues:
     * - new Guid() is not from Unity randomness!!
     * 
     * Theories:
     * - Inconsistent update order (e.g. due to hash codes/buckets)
     * - Something isn't saved in the save state (e.g. when to go to bed),
     *   so we get different behavior.
     * - Nondeterminism in the code, e.g. inconsistent dictionary traversal
     * 
     * Ruled out
     * - Unaccounted for calls to Unity random: there were no abnormal
     * calls at the time of desync, so the state was altered beforehand.
     */

    [HarmonyPatch(typeof(TickableEntityBucket), nameof(TickableEntityBucket.TickAll))]
    public class TEBPatcher
    {
        static void Prefix(TickableEntityBucket __instance)
        {
            string o = "";
            for (int i = 0; i < __instance._tickableEntities.Count; i++)
            {
                var entity = __instance._tickableEntities.Values[i];
                o += $"Will tick: {entity._originalName}, {entity.EntityId}\n";
            }
            Plugin.Log(o);
        }
    }

    //[HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.SpawnNewResources))]
    //public class NRRPatcher
    //{
    //    static void Prefix(NaturalResourceReproducer __instance)
    //    {
    //        foreach (var (reproducibleKey, coordinates) in __instance._newResources)
    //        {
    //            Plugin.LogWarning($"{reproducibleKey.Id}, {coordinates.ToString()}");
    //        }
    //    }
    //}
}
