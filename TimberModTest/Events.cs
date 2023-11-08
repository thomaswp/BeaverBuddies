using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Timberborn.Coordinates;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;

namespace TimberModTest
{
    public interface IReplayContext
    {

    }

    public abstract class ReplayEvent : IComparable<ReplayEvent>
    {
        public float timeInFixedSecs;

        public string type => GetType().Name;

        //public ReplayEvent(float timeInFixedSecs)
        //{
        //    this.timeInFixedSecs = timeInFixedSecs;
        //}

        public int CompareTo(ReplayEvent other)
        {
            if (other == null)
                return 1;
            return timeInFixedSecs.CompareTo(other.timeInFixedSecs);
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
            throw new NotImplementedException();
        }
    }


    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.Place))]
    public class PlacePatcher
    {
        static bool Prefix(BlockObject prefab, Vector3Int coordinates, Orientation orientation)
        {
            Plugin.Log($"Placing {prefab.name}, {coordinates}, {orientation}");

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
