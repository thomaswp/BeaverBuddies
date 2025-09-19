using HarmonyLib;
using System;
using System.Reflection;
using Timberborn.TickSystem;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Fix for TickOnlyArray crash during saves.
    /// Patches TickOnlyArrayService.AllowEdit to return true during save operations.
    /// </summary>
    [HarmonyPatch(typeof(TickOnlyArrayService), nameof(TickOnlyArrayService.AllowEdit), MethodType.Getter)]
    class TickOnlyArrayServiceAllowEditPatch
    {
        static bool Prefix(ref bool __result)
        {
            // Allow edit access during save operations since saves only need read access
            // but the poorly designed API requires write permission for read operations
            if (GameSaveHelper.IsSavingDeterministically || GameSaverSavePatcher.IsSaving)
            {
                __result = true;
                return false; // Skip original method
            }

            // Fall back to original method for normal operations
            return true;
        }
    }
}