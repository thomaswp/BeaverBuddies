using HarmonyLib;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Timberborn.Animations;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.CharacterMovementSystem;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
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
using Timberborn.StockpileVisualization;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using TimberNet;
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

    [HarmonyPatch(typeof(StockpileGoodPileVisualizer), nameof(StockpileGoodPileVisualizer.Awake))]
    public class StockpileGoodPileVisualizerPatcher
    {
        static void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(StockpileGoodPileVisualizerPatcher), true);
        }

        static void Postfix()
        {
            DeterminismController.SetNonGamePatcherActive(typeof(StockpileGoodPileVisualizerPatcher), false);
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

    [HarmonyPatch(typeof(System.Guid), nameof(System.Guid.NewGuid))]
    public class GuidPatcher
    {
        static bool Prefix(ref System.Guid __result)
        {
            byte[] guid = new byte[16];
            for (int i = 0; i < guid.Length; i++)
            {
                guid[i] = (byte)UnityEngine.Random.Range(0, byte.MaxValue + 1);
            }
            __result = new System.Guid(guid);
            Plugin.LogWarning($"Generating new GUID: {__result}");
            return false;
        }
    }

    /*
     * When sleep occurs, the game seems to desync.
     * 
     * Definite issues:
     * - With test map, I'm seeing consistent desync ~ tick 320. Not sure why
     *   and could be coincidence, but may be causing position divergence.
     *   The first thing that seems to desync is the random seed, suggesting
     *   either that we aren't detecting some random events *or* that something
     *   happens on that frame that causes events to happen out of order.
     *   This problem doesn't seem to occur when I force a full tick every update
     *   suggesting that updating in the middle of a tick can cause.
     *   However, there's still a desync even with 1-tick-per-update so it
     *   doesn't seem to be the only issue.
     * 
     * Theories:
     * - Some code likely still uses Time.deltaTime or Time.time, which will
     *   lead to inconsistent behavior. This shows up first in position changes.
     * - Something isn't saved in the save state (e.g. when to go to bed),
     *   so we get different behavior.
     * - Nondeterminism in the code, e.g. inconsistent dictionary traversal
     * - Floating point rounding issues with movement, etc.
     * 
     * Monitoring:
     * - SlotManager: Was the odd event out once, but can't reproduce it
     *   so probably a coincidence. Nothing in it seemed to use nondeterminism.
     * - Movement of Beavers diverges over time, possibly due to rounding
     *   or more likely something with a non-fixed-time-update.
     *   The differences start small and grow over time.
     *   I think I've fixed this. Movement still diverges, but it seems
     *   to only happen after the random state changes, so maybe no longer
     *   the main cause. Need to verify though. I've also verified that
     *   removing the MovementAnimator.Update (smooth movement) doesn't
     *   remove the movement desync (though it does change from frame 320).
     * 
     * Ruled out
     * - Unaccounted for calls to Unity random: there were no abnormal
     * calls at the time of desync, so the state was altered beforehand.
     * - new Guids are created on load after save, and before randomness
     *   is synced, but this seems to just be the patching.
     * - Inconsistent update order (e.g. due to hash codes/buckets). Seems to
     *   be consistent.
     * 
     * Fixed:
     * - Guid.NewGuid now uses Unity's random generator and is deterministic
     * - When a new entity is created, the GUID should be deterministic, and
     *   it should be added deterministically to the TickableBucketService
     * - When Entities are created, the tick should fully complete (without
     *   starting any new ticks) so the entity can Start() on the next Update().
     */

    [HarmonyPatch(typeof(TickableEntityBucket), nameof(TickableEntityBucket.Add))]
    public class TEBAddPatcher
    {

        static void Postfix(TickableEntityBucket __instance, TickableEntity tickableEntity)
        {
            if (!ReplayService.IsLoaded) return;
            int index = __instance._tickableEntities.Values.IndexOf(tickableEntity);
            Plugin.Log($"Adding: {tickableEntity.EntityId} at index {index}");
            //Plugin.LogStackTrace();
        }
    }

    [HarmonyPatch(typeof(Time), nameof(Time.time), MethodType.Getter)]
    public class TimeTimePatcher
    {
        private static float time = 0;

        public static void Tick()
        {
            time += Time.fixedDeltaTime;
        }

        static bool Prefix(ref float __result)
        {
            __result = time;
            return false;
        }
    }

    [HarmonyPatch(typeof(TickableEntityBucket), nameof(TickableEntityBucket.TickAll))]
    public class TEBPatcher
    {
        public static int EntityUpdateHash { get; private set; }
        public static int PositionHash { get; private set; }

        static void Prefix(TickableEntityBucket __instance)
        {
            for (int i = 0; i < __instance._tickableEntities.Count; i++)
            {
                var entity = __instance._tickableEntities.Values[i];
                EntityUpdateHash = TimberNetBase.CombineHash(EntityUpdateHash, entity.EntityId.GetHashCode());


                var entityComponent = entity._entityComponent;
                var pathFollower = entityComponent.GetComponentFast<Walker>()?._pathFollower;
                var animatedPathFollower = entityComponent.GetComponentFast<MovementAnimator>()?._animatedPathFollower;
                if (pathFollower != null && animatedPathFollower != null)
                {
                    // Update the animated path follower to the path follower's
                    // (hopefully) deterministic position
                    animatedPathFollower.CurrentPosition = pathFollower._transform.position;
                    PositionHash = TimberNetBase.CombineHash(PositionHash, animatedPathFollower.CurrentPosition.GetHashCode());
                }
                // Make sure it updates the model's position as well
                entityComponent.GetComponentFast<MovementAnimator>()?.UpdateTransform(0);

                // Update entity positions before the tick
                //var animator = entity._entityComponent.GetComponentFast<MovementAnimator>();
                //if (animator)
                //{
                //    var pathFollower = animator._animatedPathFollower;
                //if (pathFollower._pathCorners.Count > 0)
                //{

                //    var positionBefore = pathFollower.CurrentPosition;

                //    // Update to beyond the end of this tick to ensure the transform
                //    // is at the very end of the path for this tick
                //    var futureTime = Time.time + Time.fixedDeltaTime;
                //    pathFollower.Update(futureTime);
                //    animator.UpdateTransform(Time.fixedDeltaTime);

                //    var currentPos = pathFollower.CurrentPosition;
                //    var lastCorner = pathFollower._pathCorners.Last();
                //    if (currentPos != lastCorner.Position)
                //    {
                //        Plugin.LogWarning($"Failed to move to end of path.\n" +
                //            $"Before:     {FVS(positionBefore)}\n" +
                //            $"Current:    {FVS(currentPos)}\n" +
                //            $"LastCorner: {FVS(lastCorner.Position)}\n" +
                //            $"Start time: {pathFollower._pathCorners[0].TimeInSeconds}\n" +
                //            $"End time:   {lastCorner.TimeInSeconds}\n" +
                //            $"End time:   {lastCorner.TimeInSeconds}\n" +
                //            $"Set to:     {}");
                //    }
                //}
                //}

                //if (entity._originalName == "BeaverAdult(Clone)" || entity._originalName == "BeaverChild(Clone)")
                //{
                //    var transform = entity._entityComponent.TransformFast;
                //    Plugin.Log($"{entity.EntityId}: {transform.position} {transform.position.x}");
                //}
            }
        }

        private static string FVS(Vector3 vector)
        {
            return $"{vector.x:F2}, {vector.y:F2}, {vector.z:F2}";
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
