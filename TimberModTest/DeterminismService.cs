using HarmonyLib;
using System.Diagnostics;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoundSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using UnityEngine;

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
        public static bool IsInInputUpdate = false;
        public static bool IsPlayingSound = false;
        public static bool IsTicking = false;

        public static bool ShouldFreezeSeed 
        {
            get
            {
                // If there's randomness occurring outside of a tick
                // that isn't already accounted for, we should log it!
                if (!IsTicking && !(IsPlayingSound || IsInInputUpdate))
                {
                    LogUnknownRandomCalled();
                }

                // Freeze the random seed outside of ticking, as well as
                // for any input- or sound-related event
                return !IsTicking || IsPlayingSound || IsInInputUpdate;
            }
        }
        
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
            
            Plugin.Log("Unknown random called outside of tick");
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
            //state = Random.state;
            DeterminismController.IsInInputUpdate = true;
        }

        static void Postfix()
        {
            //Random.state = state;
            DeterminismController.IsInInputUpdate = false;
        }
    }

    [HarmonyPatch(typeof(Sounds), nameof(Sounds.GetRandomSound))]
    public class SoundsPatcher
    {
        static void Prefix()
        {
            DeterminismController.IsPlayingSound = true;
        }

        static void Postfix()
        {
            DeterminismController.IsPlayingSound = false;
        }
    }

    [HarmonyPatch(typeof(SoundEmitter), nameof(SoundEmitter.Update))]
    public class SoundEmitterPatcher
    {
        static void Prefix()
        {
            DeterminismController.IsPlayingSound = true;
        }

        static void Postfix()
        {
            DeterminismController.IsPlayingSound = false;
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

}
