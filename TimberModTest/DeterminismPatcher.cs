using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.GameScene;
using Timberborn.InputSystem;
using Timberborn.PlantingUI;
using Timberborn.SoundSystem;

namespace TimberModTest
{
    class NonGamePatcher
    {
        Type type;

        public NonGamePatcher(Type type)
        {
            this.type = type;
        }

        public void Prefix()
        {
            DeterminismController.SetNonGamePatcherActive(type, true);
        }

        public void Postfix()
        {
            DeterminismController.SetNonGamePatcherActive(type, false);
        }
    }

    // TODO: Need to figure out about water, which happens in parallel
    // and therefore not in a deterministic order, whether it uses any
    // randomness. If so, that could throw things off.
    // Could solve by pulling a string of random numbers at the start
    // of the parallel tick for it to use each time (more than it could use)
    // and it can use them in order.
    // Look at WaterSimulationController

    // TODO: Can't figure out how to create a dynamic patch
    // and it's not a high priority right now, so not using this
    public static class DeterminismPatcher
    {
        public static void PatchDeterminism(Harmony harmony)
        {
            Dictionary<System.Type, string> nonGameRandomMethods = new Dictionary<System.Type, string>()
            {
                { typeof(InputService), nameof(InputService.Update) },
                { typeof(Sounds), nameof(Sounds.GetRandomSound) },
                { typeof(SoundEmitter), nameof(SoundEmitter.Update) },
                { typeof(DateSalter), nameof(DateSalter.GenerateRandomNumber) },
                { typeof(PlantableDescriber), nameof(PlantableDescriber.GetPreviewFromPrefab) }
            };

            foreach (var type in nonGameRandomMethods.Keys)
            {
                string methodName = nonGameRandomMethods[type];

                //var prefix = typeof(DeterminismPatcher).GetMethod(nameof(DeterminismPatcher.PrefixRandom), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                //var postfix = typeof(DeterminismPatcher).GetMethod(nameof(DeterminismPatcher.PostfixRandom), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                var original = AccessTools.Method(type, methodName);
                NonGamePatcher patcher = new NonGamePatcher(type);

                var mPrefix = SymbolExtensions.GetMethodInfo(() => patcher.Prefix());
                var mPostfix = SymbolExtensions.GetMethodInfo(() => patcher.Postfix());
                harmony.Patch(original, new HarmonyMethod(mPrefix), new HarmonyMethod(mPostfix));
            }
        }
    }
}
