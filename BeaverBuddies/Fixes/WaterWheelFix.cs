using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Analytics;
using Timberborn.MechanicalSystem;
using Timberborn.MechanicalSystemUI;
using UnityEngine;

namespace BeaverBuddies.Fixes
{
    public class WaterWheelFix
    {
        [HarmonyPatch(typeof(MechanicalNodeFacingMarkerDrawer), nameof(MechanicalNodeFacingMarkerDrawer.GetTransput))]
        public class MechanicalNodeFacingMarkerDrawerGetTransputPatcher
        {
            private static bool doBase = false;
            static bool Prefix(MechanicalNode otherNode, Vector3Int coordinates, Transput transput, ref Transput __result)
            {
                if (doBase)
                {
                    // If calling the base function, just return true without doing anything
                    return true;
                }

                // Otherwise, try calling the base function, and default to null
                try
                {
                    doBase = true;
                    __result = MechanicalNodeFacingMarkerDrawer.GetTransput(otherNode, coordinates, transput);
                }
                catch
                {
                    Plugin.Log("Catching Transput UI bug...");
                    __result = null;
                }
                finally { doBase = false; }
                return false;
            }
        }
    }
}
