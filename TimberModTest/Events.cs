using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Timberborn.Coordinates;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;

namespace TimberModTest
{
    public interface IReplayContext
    {
        T GetSingleton<T>();
    }

    public abstract class ReplayEvent : IComparable<ReplayEvent>
    {
        public float timeInFixedSecs;
        public int ticksSinceLoad;

        public string type => GetType().Name;

        //public ReplayEvent(float timeInFixedSecs)
        //{
        //    this.timeInFixedSecs = timeInFixedSecs;
        //}

        public int CompareTo(ReplayEvent other)
        {
            if (other == null)
                return 1;
            //return timeInFixedSecs.CompareTo(other.timeInFixedSecs);
            return ticksSinceLoad.CompareTo(other.ticksSinceLoad);
        }

        public void SetTime(TickWathcerService tickWathcerService)
        {
            if (tickWathcerService == null) return;
            timeInFixedSecs = tickWathcerService.TotalTimeInFixedSecons;
            ticksSinceLoad = tickWathcerService.TicksSinceLoad;
        }

        public abstract void Replay(IReplayContext context);
    }

    [Serializable]
    public class BuildingPlacedEvent : ReplayEvent
    {
        public string prefab;
        public Vector3Int coordinates;
        public Orientation orientation;

        //public BuildingPlacedEvent(float timeInFixedSecs, string prefab, Vector3Int coordinates, Orientation orientation) : base(timeInFixedSecs)
        //{
        //    this.prefab = prefab;
        //    this.coordinates = coordinates;
        //    this.orientation = orientation;
        //}

        public override void Replay(IReplayContext context)
        {
            var buildingPrefab = context.GetSingleton<BuildingService>().GetBuildingPrefab(prefab);
            var blockObject = buildingPrefab.GetComponentFast<BlockObject>();
            var placer = context.GetSingleton<BlockObjectPlacerService>().GetMatchingPlacer(blockObject);
            placer.Place(blockObject, coordinates, orientation);
        }
    }


    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.Place))]
    public class PlacePatcher
    {
        static bool Prefix(BlockObject prefab, Vector3Int coordinates, Orientation orientation)
        {
            Plugin.Log($"Placing {prefab.name}, {coordinates}, {orientation}");

            //System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            //Plugin.Log(t.ToString());

            ReplayService.RecordEvent(new BuildingPlacedEvent()
            {
                prefab = prefab.name,
                coordinates = coordinates,
                orientation = orientation,
            });

            // TODO: For reader, return false
            return true;
        }
    }
}
