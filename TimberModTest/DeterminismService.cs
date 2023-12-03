using HarmonyLib;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameScene;
using Timberborn.InputSystem;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.NeedSystem;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;
using Timberborn.SoundSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using UnityEngine;
using static UnityEngine.UIElements.UIR.Allocator2D;
using static UnityEngine.UIElements.UIR.Implementation.UIRStylePainter;

namespace TimberModTest
{
    public class DeterminismService
    {
        EventBus _eventBus;

        DeterminismService(EventBus eventBus, IRandomNumberGenerator gen)
        {
            _eventBus = eventBus;
            eventBus.Register(this);
        }

        // TODO: For some reason this is still necessary. I don't know if it
        // works because of the first time (which happens before PostLoad)
        // or the second time (which happens after PostLoad). Could be either
        // depending on when the first random thing happens.
        // Simple idea: ignore this if tick > 0
        [OnEvent]
        public void OnSpeedEvent(CurrentSpeedChangedEvent e)
        {
            if (e.CurrentSpeed != 0)
            {

                Plugin.Log($"Speed changed to: {e.CurrentSpeed}; random reset");
                Random.InitState(1234);
            }
        }
    }

    //[HarmonyPatch(typeof(Random), nameof(Random.InitState))]
    //public class RandomPatcher
    //{
    //    static void Prefix()
    //    {
    //        Plugin.Log($"Random.InitState");
    //        Plugin.LogStackTrace();

    //    }
    //}

    //[HarmonyPatch(typeof(GameSaveDeserializer), nameof(GameSaveDeserializer.Load))]
    //public class LoadPatcher
    //{
    //    static void Prefix()
    //    {
    //        Plugin.Log($"GameSaveDeserializer.Load");
    //        Plugin.LogStackTrace();
    //    }
    //}

    public static class DeterminismController
    {
        private static HashSet<System.Type> activeNonGamePatchers = new HashSet<System.Type>();
        //private static HashSet<System.Type> activeNonTickGamePatchers = new HashSet<System.Type>();
        public static bool IsTicking = false;

        public static bool ShouldFreezeSeed 
        {
            get
            {
                bool areActiveNonGamePatchers = activeNonGamePatchers.Count > 0;
                //bool areActiveGamePatchers = activeNonTickGamePatchers.Count > 0;

                //if (areActiveNonGamePatchers && areActiveGamePatchers)
                //{
                //    // Should never happen, but just in case...
                //    Plugin.Log("Somehow in both non-game and game code?");
                //    Plugin.LogStackTrace();
                //    return false;
                //}

                // TODO: Actually, any time this happens, it should be happening
                // during a tick, so once I add that logic, I can remove this logic
                // and just log any game code that's running outside of a tick
                // since it shouldn't be, whether random or not!
                // If this is game code, use game random
                //if (areActiveGamePatchers) return false;



                // If this is non-game code, don't use the Game's random
                if (areActiveNonGamePatchers) return true;

                // If we're ticking, it's likely game code, and hopefully
                // we've caught any non-game code that can run during a tick!
                if (IsTicking) return false;

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
}
