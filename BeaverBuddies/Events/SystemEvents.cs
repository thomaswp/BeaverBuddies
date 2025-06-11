using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Autosaving;
using Timberborn.TimeSystem;

namespace BeaverBuddies.Events
{
    [Serializable]
    public class AutosaveEvent : ReplayEvent
    {
        public override void Replay(IReplayContext context)
        {
            // We only defer non-instant saves
            context.GetSingleton<Autosaver>().Save(false);
        }
        public override string ToActionString()
        {
            return $"Queuing autosave";
        }
    }

    // The issue isn't with autosaves specifically - it's with
    // saving... I think there's a good chance it doesn't actually
    // change the game state but it does add a spurious trace, causing
    // desyncs only because Debug=True. Will disable the trace during saving
    // and see if further desyncs happen.

    //[HarmonyPatch(typeof(Autosaver), nameof(Autosaver.Save))]
    //class AutosaverSavePatcher
    //{
    //    public static bool Prefix(bool instant)
    //    {
    //        if (instant) return true;
    //        // The Client should never autosave, so skip
    //        if (EventIO.Get() is ClientEventIO) return false;
    //        return ReplayEvent.DoPrefix(() =>
    //        {
    //            return new AutosaveEvent();
    //        });
    //    }
    //}
}
