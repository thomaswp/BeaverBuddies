using HarmonyLib;
using System.Diagnostics;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.SingletonSystem;
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


}
