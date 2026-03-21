using HarmonyLib;
using System;
using Timberborn.Navigation;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Fixes for district-related crashes during building placement.
    /// The preview system can register districts/obstacles with the nav mesh
    /// while the player is placing. When the actual placement event comes back from the
    /// server, it tries to register again, causing crashes.
    /// These patches suppress duplicate registration errors.
    /// </summary>
    [HarmonyPatch]
    public static class DistrictBuildingsFix
    {
        static Exception DistrictFinalizer(Exception __exception)
        {
            if (__exception is InvalidOperationException e &&
                (e.Message.Contains("already district center") ||
                 e.Message.Contains("Can't set obstacle") ||
                 e.Message.Contains("Can't unset obstacle")))
            {
                return null;
            }
            return __exception;
        }

        [HarmonyPatch(typeof(DistrictMap), nameof(DistrictMap.AddDistrictCenter))]
        [HarmonyFinalizer]
        static Exception AddDistrictCenterFinalizer(Exception __exception) => DistrictFinalizer(__exception);

        [HarmonyPatch(typeof(DistrictObstacleService), nameof(DistrictObstacleService.SetObstacle))]
        [HarmonyFinalizer]
        static Exception SetObstacleFinalizer(Exception __exception) => DistrictFinalizer(__exception);

        [HarmonyPatch(typeof(DistrictObstacleService), nameof(DistrictObstacleService.UnsetObstacle))]
        [HarmonyFinalizer]
        static Exception UnsetObstacleFinalizer(Exception __exception) => DistrictFinalizer(__exception);
    }
}
