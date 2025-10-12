using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.TickSystem;

namespace BeaverBuddies.Fixes
{ 

  //  [ManualMethodOverwrite]
  //  /* 2025/06/26
  //      _tickableBucketService.FinishFullTick();
		//this.FullTickFinished?.Invoke(this, EventArgs.Empty);
		//_accumulatedDeltaTime = 0f;
  //   */
  //  [HarmonyPatch(typeof(Ticker), nameof(Ticker.FinishFullTick))]
  //  class TickerFinishFullTickPatch
  //  {
  //      static void Prefix(Ticker __instance)
  //      {
  //          __instance._tickableBucketService.FinishFullTick();
  //          // Skip invoking the FullTickFinished event
  //          __instance._accumulatedDeltaTime = 0f;
  //      }
  //  }

  //  [HarmonyPatch(typeof(TickableSingletonService), nameof(TickableSingletonService.FinishParallelTick))]
  //  class TickableSingletonServiceFinishParallelTickPatch
  //  {
  //      static void Postfix(TickableSingletonService __instance)
  //      {
  //          var tickingService = SingletonManager.GetSingleton<TickingService>();
  //          tickingService.
  //      }
  //  }
}
