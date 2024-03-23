// If defined, parallel actions occur on the main thread
#define NO_PARALLEL
// If defined, the game will use constant values instead of random
// numbers, making it as deterministic as possible w.r.t random
//#define NO_RANDOM

using Bindito.Core.Internal;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Timberborn.Autosaving;
using Timberborn.Beavers;
using Timberborn.BotUpkeep;
using Timberborn.CharacterMovementSystem;
using Timberborn.Common;
using Timberborn.CoreSound;
using Timberborn.EntitySystem;
using Timberborn.ForestryEffects;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameScene;
using Timberborn.GameSound;
using Timberborn.InputSystem;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.PlantingUI;
using Timberborn.RecoveredGoodSystem;
using Timberborn.Ruins;
using Timberborn.SoundSystem;
using Timberborn.StockpileVisualization;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;
using Timberborn.WaterBuildings;
using Timberborn.WorkshopsEffects;
using TimberNet;
using UnityEngine;
using static Timberborn.GameSaveRuntimeSystem.GameSaver;
using static BeaverBuddies.SingletonManager;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Timberborn.NaturalResources;
using Timberborn.BlockSystem;

namespace BeaverBuddies
{
    /*
     * Current desync issues:
     * - Multiple examples show desyncs on more complex saves, especially
     *   once water dynamics become complex.
     *   The cause seems to be that a tree is spawned in one save but not
     *   in another, which causes an immediate desync. The random seed before
     *   that spawn is synced, suggesting that the games diverge on where it is
     *   possible to spawn, or what plants can spawn.
     *   Specifically, a NaturalResource is queued by 
     *   NaturalResourceReproducer.TrySpawnNatrualResources, but then in one game
     *   it is found valid and spawned but not in another.
     *   Completely removing randomness seems to fix the problem, but it's not
     *   yet clear what specifically fixed it (is it an non-game random
     *   call or is it something causing random call order to differ).
     *   It may simply be that because under no-randomness trees are spawning always, we don't
     *   encounter the divergence that random behavior exposes (but doesn't cause).
     * 
     * Theories for unexplained desyncs:
     * - Something isn't saved in the save state (e.g. when to go to bed),
     *   so we get different behavior.
     * - Nondeterminism in the code, e.g. inconsistent dictionary traversal.
     *   I've seen no evidence of this, though. Internet suggests that this behavior
     *   is likely deterministic, just not guaranteed to be.
     * - Floating point rounding issues with movement, etc. No evidence of this so
     *   far - at least on the same OS.
     *   
     * To try:
     * - Remove all randomness
     * - Remove interpolating animations
     * - Remove all water logic (this might get complicated...)
     * 
     * Monitoring:
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
     *   starting any new ticks) so the entity can Start() on the next Update()
     * - Time.time is now deterministic. This seemed to be a primary cause of
     *   desyncs, but I never figured out exactly why.
     */

    public class DeterminismService : IResettableSingleton
    {
        public Thread UnityThread;
        private List<StackTrace> lastRandomStackTraces = new List<StackTrace>();


        public static bool IsTicking = false;
        public static bool IsNonGameplay = false;
        private static System.Random random = new System.Random();
        private static HashSet<Type> activeNonGamePatchers = new HashSet<Type>();

        public void Reset()
        {
            IsNonGameplay = false;
            IsTicking = false;
            activeNonGamePatchers.Clear();
            // No need to reset random
        }

        public DeterminismService()
        {
            RegisterSingleton(this);
        }

        public void ClearRandomStacks()
        {
            lastRandomStackTraces.Clear();
        }

        public void PrintRandomStacks()
        {
            foreach (StackTrace stack in lastRandomStackTraces)
            {
                Plugin.Log(stack.ToString());
            }
        }

        public static T GetNonGameRandom<T>(Func<T> getter)
        {
            IsNonGameplay = true;
            try
            {
                return getter();
            }
            finally
            { 
                IsNonGameplay = false; 
            }
        }


        public static bool ShouldUseNonGameRNG()
        {
            DeterminismService determinismService = GetSingleton<DeterminismService>();
            return determinismService?.ShouldFreezeSeed ?? false;
        }

        private bool ShouldFreezeSeed 
        {
            get
            {
                // Something is asking us to return false
                if (IsNonGameplay) return true;

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
                    //lastRandomStackTraces.Add(new StackTrace());
                    Plugin.Log("s0 before: " + UnityEngine.Random.state.s0.ToString("X8"));
                    var entity = TickableEntityTickPatcher.currentlyTickingEntity;
                    Plugin.Log($"Last entity: {entity?.name} - {entity?.EntityId}");
                    Plugin.LogStackTrace();
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

        [HarmonyPatch(typeof(TickableEntity), nameof(TickableEntity.Tick))]
        static class TickableEntityTickPatcher
        {
            public static EntityComponent currentlyTickingEntity = null;

            static void Prefix(TickableEntity __instance)
            {
                currentlyTickingEntity = __instance._entityComponent;
            }

            static void Postfix()
            {
                currentlyTickingEntity = null;
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
            var state = UnityEngine.Random.state;
            var value = UnityEngine.Random.insideUnitCircle;
            UnityEngine.Random.state = state;
            return value;
        }

        private void LogUnknownRandomCalled()
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
            if (DeterminismService.ShouldUseNonGameRNG())
            {
                __result = DeterminismService.Range(inclusiveMin, inclusiveMax);
                return false;
            }

#if NO_RANDOM
            __result = inclusiveMin;
            return false;
#else
            return true;
#endif
        }
    }

    [HarmonyPatch(typeof(RandomNumberGenerator), nameof(RandomNumberGenerator.Range), typeof(int), typeof(int))]
    public class RandomRangeIntPatcher
    {
        static bool Prefix(int inclusiveMin, int exclusiveMax, ref int __result)
        {
            if (DeterminismService.ShouldUseNonGameRNG())
            {
                __result = DeterminismService.Range(inclusiveMin, exclusiveMax);
                return false;
            }

#if NO_RANDOM
            __result = inclusiveMin;
            return false;
#else
            return true;
#endif
        }
    }

    [HarmonyPatch(typeof(RandomNumberGenerator), nameof(RandomNumberGenerator.InsideUnitCircle))]
    public class RandomUnitCirclePatcher
    {
        static bool Prefix(ref Vector2 __result)
        {
            if (DeterminismService.ShouldUseNonGameRNG())
            {
                __result = DeterminismService.InsideUnitCircle();
                return false;
            }

#if NO_RANDOM
            __result = Vector2.right;
            return false;
#else
            return true;
#endif
        }
    }


    class NonTickRandomNumberGenerator : IRandomNumberGenerator
    {
        private IRandomNumberGenerator baseGenerator;

        public NonTickRandomNumberGenerator(IRandomNumberGenerator baseGenerator)
        {
            this.baseGenerator = baseGenerator;
        }

        public bool CheckProbability(float normalizedProbability)
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.CheckProbability(normalizedProbability));
        }

        public T GetEnumerableElement<T>(IEnumerable<T> source)
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.GetEnumerableElement<T>(source));
        }

        public T GetListElement<T>(IReadOnlyList<T> list)
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.GetListElement<T>(list));
        }

        public T GetListElementOrDefault<T>(IReadOnlyList<T> list)
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.GetListElementOrDefault<T>(list));
        }

        public Vector2 InsideUnitCircle()
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.InsideUnitCircle());
        }

        public float Range(float inclusiveMin, float inclusiveMax)
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.Range(inclusiveMin, inclusiveMax));
        }

        public int Range(int inclusiveMin, int exclusiveMax)
        {
            return DeterminismService.GetNonGameRandom(() => baseGenerator.Range(inclusiveMin, exclusiveMax));
        }

        public bool TryGetEnumerableElement<T>(IEnumerable<T> source, out T randomElement)
        {
            DeterminismService.IsNonGameplay = true;
            bool result = baseGenerator.TryGetEnumerableElement<T>(source, out randomElement);
            DeterminismService.IsNonGameplay = false;
            return result;
        }

        public bool TryGetListElement<T>(IReadOnlyList<T> list, out T randomElement)
        {
            DeterminismService.IsNonGameplay = true;
            bool result = baseGenerator.TryGetListElement<T>(list, out randomElement);
            DeterminismService.IsNonGameplay = false;
            return result;
        }
    }

    // If random is disabled, we do not need to distinguish between
    // game and non-game random.
#if !NO_RANDOM
    // This code finds any service or entity that uses RNG
    [HarmonyPatch(typeof(ParameterProvider), nameof(ParameterProvider.GetParameters))]
    public static class ParameterProviderPatch
    {
        static NonTickRandomNumberGenerator nonTickRNG = null;

        private static HashSet<Type> blacklist = new HashSet<Type>()
        {
            typeof(BeaverTextureSetter),
            typeof(BotManufactoryAnimationController),
            typeof(BasicSelectionSound),
            typeof(DateSalter),
            typeof(GameMusicPlayer),
            typeof(NaturalResourceModelRandomizer),
            typeof(RuinModelFactory),
            typeof(RuinModelUpdater),
            typeof(LoopingSoundPlayer),
            typeof(Sounds),
            typeof(GoodColumnVariantsService),
            typeof(GoodPileVariantsService),
            typeof(StockpileGoodPileVisualizer),
            typeof(TerrainBlockRandomizer),
            typeof(ObservatoryAnimator),
            typeof(WaterInputPipeSegmentFactory),
        };

        // Currently unused - could be used for warnings on items we don't
        // recognize
        private static HashSet<Type> whitelist = new HashSet<Type>()
        {
            // I think this can only happen during tick
            typeof(TreeCutterSideRandomizer),
        };

        static HashSet<string> types = new HashSet<string>();
        static void Postfix(object[] __result, MethodBase method)
        {
            if (blacklist.Contains(method.DeclaringType))
            {
                for (int i =  0; i < __result.Length; i++)
                {
                    if (__result[i] is RandomNumberGenerator)
                    {
                        RandomNumberGenerator rng = (RandomNumberGenerator)__result[i];
                        if (nonTickRNG == null)
                        {
                            nonTickRNG = new NonTickRandomNumberGenerator(rng);
                        }
                        __result[i] = nonTickRNG;
                    }
                }
            }

            //if (__result.Any(o => o is RandomNumberGenerator))
            //{
            //    string name = method.DeclaringType?.FullName;
            //    if (types.Add(name))
            //    {
            //        Plugin.LogWarning($"{name}");
            //    }
            //}
        }
    }
#endif

    // TODO: Many of the following are no longer necessary, since we
    // use NonTickRandomNumberGenerator, above, with many classes.
    [HarmonyPatch(typeof(InputService), nameof(InputService.Update))]
    public class InputPatcher
    {
        //private static readonly Random random = new Random();

        //private static Random.State state;

        // Just as a test, muck random sounds!
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(InputPatcher), true);
        }

        static void Postfix()
        {
            //Random.state = state;
            DeterminismService.SetNonGamePatcherActive(typeof(InputPatcher), false);
        }
    }

    [HarmonyPatch(typeof(Sounds), nameof(Sounds.GetRandomSound))]
    public class SoundsPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(SoundsPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(SoundsPatcher), false);
        }
    }

    [HarmonyPatch(typeof(SoundEmitter), nameof(SoundEmitter.Update))]
    public class SoundEmitterPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(SoundEmitter), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(SoundEmitter), false);
        }
    }

    [HarmonyPatch(typeof(DateSalter), nameof(DateSalter.GenerateRandomNumber))]
    public class DateSalterPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(DateSalterPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(DateSalterPatcher), false);
        }
    }

    [HarmonyPatch(typeof(PlantableDescriber), nameof(PlantableDescriber.GetPreviewFromPrefab))]
    public class PlantableDescriberPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(PlantableDescriberPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(PlantableDescriberPatcher), false);
        }
    }

    [HarmonyPatch(typeof(StockpileGoodPileVisualizer), nameof(StockpileGoodPileVisualizer.Awake))]
    public class StockpileGoodPileVisualizerPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(StockpileGoodPileVisualizerPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(StockpileGoodPileVisualizerPatcher), false);
        }
    }

    [HarmonyPatch(typeof(RecoveredGoodStackFactory), nameof(RecoveredGoodStackFactory.RandomizeRotation))]
    public class RecoveredGoodStackFactoryPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(RecoveredGoodStackFactoryPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(RecoveredGoodStackFactoryPatcher), false);
        }
    }

   // TODO: Timberborn.GameLibs is out of date. This should come from the WorkshopEffets namespace
   // [HarmonyPatch(typeof(ObservatoryAnimator), nameof(ObservatoryAnimator.GenerateRandomAngles))]
   // public class ObservatoryAnimatorGenerateRandomAnglesPatcher
   // {
   //     static void Prefix()
   //     {
   //         DeterminismController.SetNonGamePatcherActive(typeof(ObservatoryAnimatorGenerateRandomAnglesPatcher), true);
   //     }

   //     static void Postfix()
   //     {
   //         DeterminismController.SetNonGamePatcherActive(typeof(ObservatoryAnimatorGenerateRandomAnglesPatcher), false);
   //     }
   // }

    [HarmonyPatch(typeof(LoopingSoundPlayer), nameof(LoopingSoundPlayer.PlayLooping))]
    public class LoopingSoundPlayerPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(LoopingSoundPlayerPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(LoopingSoundPlayerPatcher), false);
        }
    }

    [HarmonyPatch(typeof(BotManufactoryAnimationController), nameof(BotManufactoryAnimationController.ResetRingRotation))]
    public class BotManufactoryAnimationControllerPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(BotManufactoryAnimationControllerPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(BotManufactoryAnimationControllerPatcher), false);
        }
    }

    [HarmonyPatch(typeof(TerrainBlockRandomizer), nameof(TerrainBlockRandomizer.PickVariation))]
    public class TerrainBlockRandomizerPickVariationPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(TerrainBlockRandomizerPickVariationPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(TerrainBlockRandomizerPickVariationPatcher), false);
        }
    }


    [HarmonyPatch(typeof(BeaverTextureSetter), nameof(BeaverTextureSetter.Start))]
    public class BeaverTextureSetterStartPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(BeaverTextureSetterStartPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(BeaverTextureSetterStartPatcher), false);
        }
    }

    [HarmonyPatch(typeof(TickableBucketService), nameof(TickableBucketService.FinishFullTick))]
    static class TickableBucketService_FinishFullTick_Patch
    {
        static bool Prefix(TickableBucketService __instance)
        {
            if (EventIO.IsNull) return true;
            // If we're saving, ignore this - we've ensured a full
            // tick was completed beforehand
            if (GameSaverSavePatcher.IsSaving) return false;
            // Otherwise log it - we need to investigate this
            Plugin.LogWarning("Finishing full tick - this probably is bad!");
            Plugin.LogStackTrace();
            return true;
        }
    }

    // TODO: Double check that this still works - the arguments have
    // changed and now it gets saved on LateUpdate as a queueing process
    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(GameSaver), nameof(GameSaver.Save), typeof(QueuedSave))]
    public class GameSaverSavePatcher
    {
        public static bool IsSaving { get; set; }

        static bool Prefix(GameSaver __instance, QueuedSave queuedSave)
        {
            if (IsSaving || EventIO.IsNull) return true;
            TickingService ts = GetSingleton<TickingService>();
            if (ts == null) return true;
            ts.FinishFullTickAndThen(() =>
            {
                IsSaving = true;
                __instance.Save(queuedSave);
                IsSaving = false;
            });
            return false;
        }
    }

    [HarmonyPatch(typeof(Autosaver), nameof(Autosaver.CreateExitSave))]
    public class AutosaverCreateExitSavePatcher
    {

        static void Prefix()
        {
            // Go straight to saving since we're going to exit
            // and don't need to keep clients in sync
            GameSaverSavePatcher.IsSaving = true;
        }
    }


    [HarmonyPatch(typeof(Ticker), nameof(Ticker.Update))]
    public class TickerPatcher
    {
        static void Prefix()
        {
            DeterminismService.IsTicking = true;
        }
        static void Postfix()
        {
            DeterminismService.IsTicking = false;
        }
    }

    [HarmonyPatch(typeof(Guid), nameof(Guid.NewGuid))]
    public class GuidPatcher
    {
        static bool Prefix(ref Guid __result)
        {
#if NO_RANDOM
            __result = GenerateIncrementally();
#else
            __result = GenerateWithUnityRandom();
#endif
            if (ReplayService.IsLoaded)
            {
                Plugin.Log($"Generating new GUID: {__result}");
            }
            return false;
        }

        static long nextGuid = 0;
        private static Guid GenerateIncrementally()
        {
            byte[] bytes = BitConverter.GetBytes(nextGuid++);
            byte[] guid = new byte[16];
            Array.Copy(bytes, guid, bytes.Length);
            return new Guid(guid);
        }

        private static Guid GenerateWithUnityRandom()
        {
            byte[] guid = new byte[16];
            for (int i = 0; i < guid.Length; i++)
            {
                guid[i] = (byte)UnityEngine.Random.Range(0, byte.MaxValue + 1);
            }
            return new Guid(guid);
        }
    }

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

        public static void SetTicksSinceLoaded(int ticks)
        {
            time = ticks * Time.fixedDeltaTime;
        }

        static bool Prefix(ref float __result)
        {
            if (EventIO.IsNull) return true;
            __result = time;
            return false;
        }
    }

    //[HarmonyPatch(typeof(Walker), nameof(Walker.FindPath))]
    //public class WalkerFindPathPatcher
    //{

    //    static void Prefix(Walker __instance, IDestination destination)
    //    {
    //        string entityID = __instance.GetComponentFast<EntityComponent>().EntityId.ToString();
    //        if (destination is PositionDestination)
    //        {
    //            Plugin.Log($"{entityID} going to: " +
    //                $"{((PositionDestination)destination).Destination}");
    //        } 
    //        else if (destination is AccessibleDestination)
    //        {
    //            var accessible = ((AccessibleDestination)destination).Accessible;
    //            Plugin.Log($"{entityID} going to: " +
    //                $"{accessible.GameObjectFast.name}");
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(TickableEntityBucket), nameof(TickableEntityBucket.TickAll))]
    public class TEBPatcher
    {
        public static int EntityUpdateHash { get; private set; }
        public static int PositionHash { get; private set; }

        public static void SetHashes(int entityUpdateHash, int positionHash)
        {
            EntityUpdateHash = entityUpdateHash;
            PositionHash = positionHash;
        }

        static void Prefix(TickableEntityBucket __instance)
        {
            if (EventIO.IsNull) return;

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
                    var targetPos = pathFollower._transform.position;
                    animatedPathFollower.CurrentPosition = targetPos;
                    PositionHash = TimberNetBase.CombineHash(PositionHash, targetPos.GetHashCode());
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
                //    Plugin.Log($"{entity.EntityId}: {FVS(transform.position)}");
                //}
            }
        }

        private static string FVS(Vector3 vector)
        {
            return $"({vector.x}, {vector.y}, {vector.z})";
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

#if NO_PARALLEL
    [HarmonyPatch(typeof(TickableSingletonService), nameof(TickableSingletonService.StartParallelTick))]
    [ManualMethodOverwrite]
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
                parallelTickable.ParallelTick();
            }
            return false;
        }
    }
#endif

    [HarmonyPatch(typeof(SpawnValidationService), nameof(SpawnValidationService.CanSpawn))]
    [ManualMethodOverwrite]
    class SpawnValidationServiceCanSpawnPatcher
    {
        static void Postfix(SpawnValidationService __instance, bool __result, Vector3Int coordinates, Blocks blocks, string resourcePrefabName)
        {
            Plugin.LogWarning($"Trying to spawn {resourcePrefabName} at {coordinates}: {__result}\n" +
                $"IsSuitableTerrain: {__instance.IsSuitableTerrain(coordinates)}\n" +
                $"SpotIsValid: {__instance.SpotIsValid(coordinates, resourcePrefabName)}\n" +
                $"IsUnobstructed: {__instance.IsUnobstructed(coordinates, blocks)}");
        }
    }
}
