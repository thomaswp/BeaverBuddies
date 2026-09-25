using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.TerrainQueryingSystem;
using UnityEngine;

namespace BeaverBuddies.Util
{
    public static class WorldPointPicker
    {
        /**
         * Finds the world position under the given screen point, falling
         * back to the y=0 plane so points off the map still resolve.
         */
        public static bool TryPick(CameraService cameraService, TerrainPicker terrainPicker,
            Vector2 screenPoint, out Vector3 worldPos)
        {
            Ray gridRay = cameraService.ScreenPointToRayInGridSpace(screenPoint);
            var hit = terrainPicker.PickTerrainCoordinates(gridRay);
            if (hit.HasValue)
            {
                worldPos = CoordinateSystem.GridToWorld(hit.Value.Intersection);
                return true;
            }

            Ray worldRay = cameraService.ScreenPointToRayInWorldSpace(screenPoint);
            if (worldRay.direction.y >= -0.0001f)
            {
                worldPos = default;
                return false;
            }
            float t = -worldRay.origin.y / worldRay.direction.y;
            worldPos = worldRay.origin + worldRay.direction * t;
            return true;
        }
    }
}
