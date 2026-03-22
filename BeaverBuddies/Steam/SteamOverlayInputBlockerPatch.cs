using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.SteamOverlaySystem;
using Timberborn.SteamStoreSystem;

namespace BeaverBuddies.Steam
{
    [HarmonyPatch(typeof(SteamOverlayInputBlocker), nameof(SteamOverlayInputBlocker.SteamOverlayActivated))]
    internal class SteamOverlayInputBlockerPatch
    {
        static bool Prefix(SteamOverlayInputBlocker __instance, GameOverlayActivated_t callback)
        {
            // If closing the UI and we'd normally pop the panel...
            if (callback.m_nAppID == SteamAppId.AppId && callback.m_bActive != 1)
            {
                // If the stack is empty (e.g. because we've loaded a map and the stack was cleared),
                // stop the base method from running.
                var stack = __instance._panelStack._stack;
                if (stack.Count == 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
