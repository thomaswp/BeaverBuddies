// If defined, parallel actions occur on the main thread
//#define NO_PARALLEL
// If defined, the game will use constant values instead of random
// numbers, making it as deterministic as possible w.r.t random
//#define NO_RANDOM

using BeaverBuddies.DesyncDetecter;
using BeaverBuddies.IO;
using Bindito.Core.Internal;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Timberborn.Analytics;
using Timberborn.Autosaving;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BotUpkeep;
using Timberborn.Brushes;
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
using Timberborn.TerrainSystemRendering;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.ToolSystem;
using Timberborn.WalkingSystem;
using Timberborn.WaterBuildings;
using Timberborn.WorkshopsEffects;
using TimberNet;
using Unity.Services.Analytics;
using UnityEngine;
using static BeaverBuddies.SingletonManager;
using static Timberborn.GameSaveRuntimeSystem.GameSaver;

namespace BeaverBuddies
{
    /*
     * Current desync issues:
     * - *knock on wood*
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
     * - Use Debug mode in the config, and add more Trace calls to pinpoint issues.
     * - Remove all randomness
     * - Remove interpolating animations
     * - Remove all water logic (this might get complicated...)
     * - Log random calls during load to look for non-gameplay logic
     * 
     * Known Issues:
     * - Either NaturalResourceReproducer.TryReprosuceResources (less likely)
     *   or WateredNaturalResource.StartDryingOut (more likely) is desyncing,
     *   and if the later it's likely because of a timer being created.
     *   Time logging produces lots of false positives, so I don't typically
     *   do it, but I could try to find a way to...
     * 
     * Monitoring:
     * - There may be other Singleton's with game logic updates.
     * - Some TimeTriggers seem to happen not completely synchronized, but
     *   all the ones I've observed are for non-game logic so far.
     * - Game state seems to be synced at load now, but need to further confirm.
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
     * - Beaver movement logic is now only updated on tick, with a separate
     *   render-only update logic in AnimationFixes, which is undone before a
     *   tick starts.
     * - WateredNaturalResource.Awake() and LivingWaterNaturalResource.Awake()
     *   all random, which likely occurs before the client receives its RNG. 
     *   Further, the game saves the *progress* towards death, rather than the time 
     *   of death, so it would be hard to reload.
     *   Should be fixed by having original random state based on map hash for both
     *   Server and Clients.
     * - A number of Singletons have game logic in their UpdateSingleton method.
     *   I have moved these to TickReplacerService's Tick method to keep it synced.
     */

    public class DeterminismService : RegisteredSingleton, IResettableSingleton
    {
        public Thread UnityThread;

        public static bool IsTicking = false;
        public static bool IsNonGameplay = false;
        private static System.Random random = new System.Random();
        private static HashSet<Type> activeNonGamePatchers = new HashSet<Type>();
        private static int? nextSeedOnLoad;

        public void Reset()
        {
            IsNonGameplay = false;
            IsTicking = false;
            activeNonGamePatchers.Clear();
            // No need to reset random
            // Don't reset seed, since it's set before the Reset
        }

        public DeterminismService()
        {
            if (nextSeedOnLoad.HasValue)
            {
                Plugin.Log($"DeterminismService init with seed: {nextSeedOnLoad.Value:X8}");
                UnityEngine.Random.InitState(nextSeedOnLoad.Value);
                nextSeedOnLoad = null;
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
                // If for some reason this is happening outside of
                // a multiplayer game, we don't need to freeze the seed
                if (EventIO.IsNull) return false;

                // Something is asking us to return false
                if (IsNonGameplay) return true;


                // When the game is loading, almost all random calls are gameplay logic
                // (e.g., choosing when trees die, or choosing an Enterer).
                // I have filtered out the non-gameplay ones I've found, and even if they
                // use gameplay random, it should be ok as long as they are deterministic,
                // and not determined by UI events. This may require more monitoring.
                // So we want to use gameplay random.
                if (!ReplayService.IsLoaded)
                {
                    if (EventIO.Config.Debug)
                    {
                        DesyncDetecterService.Trace($"Load RNG; s0 before: {UnityEngine.Random.state.s0:X8}");
                    }
                    return false;
                }

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
                    if (EventIO.Config.Debug)
                    {
                        DesyncDetecterService.Trace($"Tick RNG; " +
                        $"s0 before: {UnityEngine.Random.state.s0:X8}; " +
                        $"Last entity: {entity?.name} - {entity?.EntityId}");
                    }
                    return false;
                }

                // If we are replaying/playing events recorded from this
                // user or other clients, we should always use the game's random.
                // These mostly happen during ticks, but can also happen
                // when the game is paused.
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

        public static void InitGameStartState(byte[] mapBytes)
        {
            int state = 13;
            for (int i = 0; i < mapBytes.Length; i++)
            {
                state = TimberNetBase.CombineHash(state, mapBytes[i]);
            }
            InitGameStartState(state);
        }

        private static void InitGameStartState(int state)
        {
            Plugin.Log($"Setting next random state: {state.ToString("X8")}");
            nextSeedOnLoad = state;
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
            typeof(BrushProbabilityMap),
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

            //if (__result.Any(o => o is CommandLineArguments))
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

    // Sometimes tool descriptions need an instance of the object they describe to describe it
    // and when it activates this can use randomness (e.g. WateredNaturalResource), which should
    // be considered UI randomness, since this object never gets in the game.
    [HarmonyPatch(typeof(DescriptionPanel), nameof(DescriptionPanel.SetDescription))]
    public class DescriptionPanelSetDescriptionPatcher
    {
        static void Prefix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(DescriptionPanelSetDescriptionPatcher), true);
        }

        static void Postfix()
        {
            DeterminismService.SetNonGamePatcherActive(typeof(DescriptionPanelSetDescriptionPatcher), false);
        }
    }

    // Disable analytics while this mod is enabled, since Unity's Analytics
    // package seems to cause a bunch of desyncs, and I'm not confident
    // my patches have fixed them.
    [HarmonyPatch(typeof(AnalyticsManager), nameof(AnalyticsManager.Enable))]
    public class AnalyticsManagerEnablePatcher
    {
        static bool Prefix()
        {
            Plugin.Log("Skipping AnalyticsManager.Enable");
            return false;
        }
    }

    [HarmonyPatch(typeof(AnalyticsContainer), nameof(AnalyticsContainer.Update))]
    public class AnalyticsContainerUpdatePatcher
    {
        static void Prefix()
        {
            Plugin.LogWarning("AnalyticsContainer.Update is being called; analytics should be disabled!");
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
                // Only trace this Guid if it would use Unity random, and that would
                // use the Game RNG.
                if (EventIO.Config.Debug && !DeterminismService.ShouldUseNonGameRNG())
                {
                    DesyncDetecterService.Trace($"Generating new GUID: {__result}");
                }
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


    [ManualMethodOverwrite]
    /*
public TimeOfDay FluidTimeOfDay => CalculateTimeOfDay(FluidSecondsPassedToday);
     */
    [HarmonyPatch(typeof(DayNightCycle), nameof(DayNightCycle.FluidSecondsPassedToday), MethodType.Getter)]
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
                var pathFollower = entityComponent.GetComponentFast<Walker>()?.PathFollower;
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
    [ManualMethodOverwrite]
    /*
9/2/202024
_parallelTickStartTimestamp = Stopwatch.GetTimestamp();
ImmutableArray<IParallelTickableSingleton>.Enumerator enumerator = _parallelTickableSingletons.GetEnumerator();
while (enumerator.MoveNext())
{
	IParallelTickableSingleton parallelTickable = enumerator.Current;
	_parallelizerContext.Run(delegate
	{
		parallelTickable.ParallelTick();
	});
}
     */
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
                parallelTickable.ParallelTick();
            }
            return false;
        }
    }
#endif

    // If there's more than ~3 of these, I could probably make a
    // generalizable approach to prevent Singletons from updating
    // and instead update them on tick.
    [HarmonyPatch(typeof(RecoveredGoodStackSpawner), nameof(RecoveredGoodStackSpawner.UpdateSingleton))]
    class RecoveredGoodStackSpawnerUpdateSingletonPatcher
    {
        private static bool doBaseUpdate = false;
        
        public static void BaseUpdateSingleton(RecoveredGoodStackSpawner __instance)
        {
            doBaseUpdate = true;
            __instance.UpdateSingleton();
            doBaseUpdate = false;
        }

        static bool Prefix(RecoveredGoodStackSpawner __instance)
        {
            if (EventIO.IsNull) return true;
            if (doBaseUpdate) return true;
            return false;
        }
    }


}
