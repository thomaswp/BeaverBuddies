using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.GameDistricts;
using Timberborn.Navigation;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Fixes for district-related crashes during building placement and destruction.
    /// The preview system can register districts/obstacles with the nav mesh
    /// while the player is placing. When the actual placement event comes back from the
    /// server, it tries to register again, causing crashes.
    /// Also fixes crashes when destroying a district center, which triggers automatic migration
    /// to relocate the population.
    /// These patches suppress duplicate registration errors and handle missing clusters.
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

        // Fix for destroyed district centers not being in any cluster
        // Destruction triggers automatic migration, but the district is already removed from clusters

        [HarmonyPatch(typeof(DistrictConnections), nameof(DistrictConnections.GetDistrictsConnectedWith))]
        [HarmonyFinalizer]
        static Exception GetDistrictsConnectedWithFinalizer(Exception __exception, ref IEnumerable<DistrictCenter> __result)
        {
            if (__exception is NotSupportedException)
            {
                __result = Enumerable.Empty<DistrictCenter>();
                return null;
            }
            return __exception;
        }

        [HarmonyPatch(typeof(DistrictConnections), nameof(DistrictConnections.AreDistrictsConnected))]
        [HarmonyFinalizer]
        static Exception AreDistrictsConnectedFinalizer(Exception __exception, ref bool __result)
        {
            if (__exception is NotSupportedException)
            {
                __result = false;
                return null;
            }
            return __exception;
        }
    }
}
