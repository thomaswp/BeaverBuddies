// If defined, parallel actions occur on the main thread
//#define NO_PARALLEL
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
using static Timberborn.NaturalResourcesReproduction.NaturalResourceReproducer;
using System.Linq;
using Timberborn.NaturalResourcesReproduction;
using Timberborn.TimeSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.SlotSystem;
using Timberborn.EnterableSystem;
using System.Collections;
using BeaverBuddies.DesyncDetecter;

namespace BeaverBuddies
{
    /*
     * Current desync issues:
     * - Multiple examples show desyncs on more complex saves, especially
     *   once water dynamics become complex.
     *   The cause seems to be that a NatrualResource is spawned in one save but not
     *   in another, which causes an immediate desync. This is caused by divergence
     *   in the set of available spots for the resource to spawn, which manifests
     *   only later when the resource spawns in two different locations.
     *   This may not be detected until a resource tries to spawn in one game
     *   but cannot in the other (because they're in two different spots).
     *   Specifically, a NaturalResource is queued by 
     *   NaturalResourceReproducer.TrySpawnNatrualResources, but then in one game
     *   it is found valid and spawned but not in another.
     *   I thought one cause was using HashSet.GetElementAt(), but turns out changing this
     *   just changed whether the bug happened *that time*.
     *   Another cause is something about how ticks are spread out over multiple updates.
     *   Forcing one tick per update now fixes the problem. Confirming this still seems to be the case.
     *   The culprit here seems to be the TimeTriggerService (and the whole system)
     *   which uses time of day rather than ticks which is likely messing things up
     *   (need to look further!).
     * 
     * 
     * Theories for unexplained desyncs:
     * - Something isn't saved in the save state (e.g. when to go to bed),
     *   so we get different behavior.
     * - Floating point rounding issues with movement, etc. No evidence of this so
     *   far - at least on the same OS.
     * - Some gameplay code that occurs in OnDestroyed (though I have seen that this
     *   can directly trigger when the object is destroyed during a tick, it may also
     *   be triggered sometimes at the end of a frame).
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
     * - Some TimeTriggers seem to happen not completely synchronized, but
     *   I believe some of these are animation things, so they don't need to
     *   be. It does not seem to be causing desyncs.
     * 
     * Ruled out
     * - Unaccounted for calls to Unity random: there were no abnormal
     * calls at the time of desync, so the state was altered beforehand.
     * - new Guids are created on load after save, and before randomness
     *   is synced, but this seems to just be the patching.
     * - Inconsistent update order (e.g. due to hash codes/buckets). Seems to
     *   be consistent.
     * - HashSet is *not* the cause of desyncs. I've looked and the source code
     *   and run a number of tests. The way it's written, the order of enumeration
     *   is independent of the hashcodes themselves, and therefore depends only
     *   on the order of additions and removals. If the rest of the game is
     *   deterministic, it should be as well.
     * 
     * Fixed:
     * - Guid.NewGuid now uses Unity's random generator and is deterministic
     * - When a new entity is created, the GUID should be deterministic, and
     *   it should be added deterministically to the TickableBucketService
     * - When Entities are created, the tick should fully complete (without
     *   starting any new ticks) so the entity can Start() on the next Update()
     * - Time.time is now deterministic. This seemed to be a primary cause of
     *   desyncs, but I never figured out exactly why.
     * - WateredNaturalResource.Awake() and LivingWaterNaturalResource.Awake()
     *   all random, which likely occurs before the client receives its RNG. 
     *   Further, the game saves the *progress* towards death, rather than the time 
     *   of death, so it would be hard to reload.
     *   Likely fixed by always initializing random (before game load) to a fixed
     *   value (would kind of be nice to do this anyway for deterministic bugs).
     */

    public class DeterminismService : IResettableSingleton
    {
        public Thread UnityThread;

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

            // Deterministic seed set before the game is ever loaded,
            // since some components call Random during load and before
            // the Client can receive a seed
            InitRandomState(42, "Pre-load");
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

        /// <summary>
        /// Returns true if the game's random seed should be "frozen," meaning
        /// a non-game RNG should be used instead.
        /// In essence this returns true if we think a random call right now
        /// is unrelated to gameplay and does not need to be synced.
        /// </summary>
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
                    var entity = TickableEntityTickPatcher.currentlyTickingEntity;
                    DesyncDetecterService.Trace($"Tick RNG; " +
                        $"s0 before: {UnityEngine.Random.state.s0:X8}; " +
                        $"Last entity: {entity?.name} - {entity?.EntityId}");
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
            if (EventIO.IsNull) return;

            Plugin.LogWarning("Unknown random called outside of tick");
            Plugin.LogStackTrace();
        }

        public static void InitRandomState(int state, string reason)
        {
            UnityEngine.Random.InitState(state);
            Plugin.Log($"[{reason}]: Initializing random with state: {state.ToString("X8")}");
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
                for (int i = 0; i < __result.Length; i++)
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

            //if (__result.Any(o => o is TimeTriggerFactory || o is TimeTriggerService))
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
        private static bool makeRealGuid = false;

        public static Guid RealNewGuid()
        {
            makeRealGuid = true;
            Guid guid = Guid.NewGuid();
            makeRealGuid = false;
            return guid;
        }

        static bool Prefix(ref Guid __result)
        {
#if NO_RANDOM
            __result = GenerateIncrementally();
#else
            if (makeRealGuid)
            {
                return true;
            }
            __result = GenerateWithUnityRandom();
#endif
            if (ReplayService.IsLoaded)
            {
                DesyncDetecterService.Trace($"Generating new GUID: {__result}");
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


    [HarmonyPatch(typeof(EntityService), nameof(EntityService.Instantiate), typeof(BaseComponent), typeof(Guid))]
    static class EntityComponentInstantiatePatcher
    {
        static void Prefix(EntityService __instance, BaseComponent prefab, ref Guid id)
        {
            if (EventIO.IsNull) return;

            var replayService = GetSingleton<ReplayService>();

            // During preloading, a GUID can be generated that already exists in 
            // the save, so this guards against duplicate GUIDs.
            // It should not happen repeatedly, but we max out (and error) if it goes
            // over 100 times.
            for (int i = 0; i < 100; i++)
            {
                var existingEntity = __instance._entityRegistry.GetEntity(id);
                if (existingEntity == null) break;
                string logMessage = $"Duplicate GUID {id} detected, generating new GUID. Attempt #{i}.";
                if (replayService != null && replayService.TicksSinceLoad > 0)
                {
                    // We only log a warning if loaded, since we do expect this to happen
                    // sometimes during preloading.
                    Plugin.LogWarning(logMessage);
                }
                else
                {
                    Plugin.Log(logMessage);
                }
                id = Guid.NewGuid();
            }
            TickingService ts = GetSingleton<TickingService>();
            if (ts != null)
            {
                // Interrupt immediately, so a frame passes before
                // the next bucket is ticked, so the Entity is deterministically
                // initialized before the next bucket is ticked.
                // Note: we do this instead of finishing a full frame to avoid
                // the game constantly skipping frames when there are lots of
                // entities created.
                ts.ShouldInterruptTicking = true;
            }
            return;
        }
    }

    [HarmonyPatch(typeof(TickableEntityBucket), nameof(TickableEntityBucket.Add))]
    public class TEBAddPatcher
    {

        static void Postfix(TickableEntityBucket __instance, TickableEntity tickableEntity)
        {
            if (!ReplayService.IsLoaded) return;
            int index = __instance._tickableEntities.Values.IndexOf(tickableEntity);
            DesyncDetecterService.Trace($"Adding: {tickableEntity.EntityId} at index {index}");
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

    // TODO: Check whether this is ever called by game logic
    // or just override it to _secondsPassedToday
    // DayNightCycle.FluidSecondsPassedToday

    [HarmonyPatch(typeof(DayNightCycle), nameof(DayNightCycle.FluidSecondsPassedToday), MethodType.Getter)]
    [ManualMethodOverwrite]
    public class DayNightCycleFluidSecondsPassedTodayPatcher
    {
        static bool Prefix(DayNightCycle __instance, ref float __result)
        {
            if (EventIO.IsNull) return true;
            //Plugin.LogStackTrace();
            // Don't add the seconds passed this tick, since that's based on update
            __result = __instance._secondsPassedToday;
            return false;
        }
    }

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

    // TODO: Eventually need to test removing this. I'm 
    // pretty sure at this point that it changes the random
    // behavior/order but doesn't fix anything.
    [HarmonyPatch(typeof(NaturalResourceReproducer), nameof(NaturalResourceReproducer.TryReproduceResources))]
    [ManualMethodOverwrite]
    class NaturalResourceReproducerTryReproduceResourcesPatcher
    {

        static bool Prefix(NaturalResourceReproducer __instance)
        {
            float num = __instance._dayNightCycle.FixedDeltaTimeInHours / 24f;
            foreach (KeyValuePair<ReproducibleKey, HashSet<Vector3Int>> potentialSpot in __instance._potentialSpots)
            {
                float num2 = num * potentialSpot.Key.ReproductionChance;
                float num3 = __instance._randomNumberGenerator.Range(0f, 1f);
                HashSet<Vector3Int> value = potentialSpot.Value;
                if (num3 < num2 * (float)value.Count)
                {
                    int index = __instance._randomNumberGenerator.Range(0, value.Count);
                    // PATCH
                    // HashSet.ElementAt() is not deterministic, so we replace it
                    // with a deterministically sorted list.
                    var potentialSpawnLocations = potentialSpot.Value.ToList();
                    potentialSpawnLocations = potentialSpawnLocations.OrderBy(v => v.x).ThenBy(v => v.y).ThenBy(v => v.z).ToList();
                    Vector3Int position = potentialSpawnLocations[index];
                    //Plugin.LogWarning($"Selecting element {index} = {position} from {potentialSpawnLocations.Count} items");
                    __instance._newResources.Add((potentialSpot.Key, position));
                    // END PATCH
                }
            }
            __instance.SpawnNewResources();

            return false;
        }
    }

    // TODO: Eventually need to test removing this. I'm 
    // pretty sure at this point that it changes the random
    // behavior/order but doesn't fix anything.
    //[HarmonyPatch(typeof(SlotManager), nameof(SlotManager.AssignFirstUnassigned))]
    //[ManualMethodOverwrite]
    //class SlotManagerAssignFirstUnassignedPatcher
    //{
    //    static bool Prefix(SlotManager __instance)
    //    {
    //        if (__instance._unassignedEnterers.Count > 0)
    //        {
    //            // PATCH
    //            // HashSet.First() is not deterministic, so we use a sorted list instead
    //            // TODO: This is slow so optimize if it helps
    //            //Enterer enterer = __instance._unassignedEnterers
    //            //    .OrderBy(e => e.GetComponentFast<EntityComponent>().EntityId)
    //            //    .FirstOrDefault();
    //            // END PATCH
    //            Enterer enterer = __instance._unassignedEnterers.First();
    //            var available = string.Join(',',
    //                __instance._unassignedEnterers
    //                    .Select(e => e.GetComponentFast<EntityComponent>().EntityId)
    //                    .OrderBy(e => e)
    //                    );
    //            Plugin.Log($"Selecting from: {available}");
    //            Plugin.Log(enterer.GetComponentFast<EntityComponent>().EntityId.ToString());
    //            __instance._unassignedEnterers.Remove(enterer);
    //            __instance.AddEnterer(enterer);
    //        }
    //        return false;
    //    }
    //}
}
