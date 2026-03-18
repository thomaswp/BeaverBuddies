#define IS_STEAM

#if IS_STEAM
using HarmonyLib;
using System;
using Timberborn.CoreUI;
using UnityEngine;

namespace BeaverBuddies.Steam
{
    /// <summary>
    /// Patches SteamOverlayInputBlocker to suppress exceptions when dialogs are shown on top.
    /// Without this, showing a dialog while overlay is active causes a crash when overlay closes.
    /// </summary>
    [HarmonyPatch(typeof(Timberborn.SteamOverlaySystem.SteamOverlayInputBlocker), "SteamOverlayActivated")]
    public class SteamOverlayInputBlockerPatch
    {
        static Exception Finalizer(Exception __exception)
        {
            // Suppress the "not on top of the stack" exception
            // This happens when we show our connection dialog while overlay is open
            if (__exception != null && __exception is ArgumentException &&
                __exception.Message.Contains("is not on top of the stack"))
            {
                Debug.Log("BeaverBuddies: Suppressed SteamOverlayInputBlocker stack exception (expected when showing dialogs during overlay)");
                return null; // Suppress the exception
            }
            return __exception;
        }
    }
}
#endif
