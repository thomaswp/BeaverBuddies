using BeaverBuddies.Events;
using BeaverBuddies.IO;
using System;
using System.Collections.Generic;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.InputSystem;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using Timberborn.TerrainQueryingSystem;
using UnityEngine;

namespace BeaverBuddies.Ping
{
    public class ActivePing
    {
        public Vector3 WorldPosition;
        public string SenderName;
        public Color Color;
        public float CreatedTime;
        public float ExpiryTime;
    }

    public class PingService : RegisteredSingleton, IPostLoadableSingleton, IInputProcessor
    {
        public const float PingDurationSeconds = 5f;
        public const string PingKeyBindingId = "BeaverBuddies.KeyBind.Ping";

        private readonly InputService _inputService;
        private readonly CameraService _cameraService;
        private readonly TerrainPicker _terrainPicker;
        private readonly LoadingScreen _loadingScreen;

        private readonly List<ActivePing> _activePings = new();

        private GameObject _overlayHost;

        public PingService(
            InputService inputService,
            CameraService cameraService,
            TerrainPicker terrainPicker,
            LoadingScreen loadingScreen)
        {
            _inputService = inputService;
            _cameraService = cameraService;
            _terrainPicker = terrainPicker;
            _loadingScreen = loadingScreen;
        }

        public IReadOnlyList<ActivePing> ActivePings => _activePings;

        public void ClearAllPings() => _activePings.Clear();

        public void PostLoad()
        {
            _overlayHost = new GameObject("BeaverBuddies_PingOverlay");
            var overlay = _overlayHost.AddComponent<PingOverlay>();
            overlay.Service = this;
            overlay.CameraService = _cameraService;

            _inputService.AddInputProcessor(this);

            // Tear down before the loading screen is shown so pings don't
            // linger over it; EventIO/scene unload happen later.
            _loadingScreen.LoadingScreenEnabled += OnLoadingScreenEnabled;
        }

        private void OnLoadingScreenEnabled(object sender, EventArgs e)
        {
            _loadingScreen.LoadingScreenEnabled -= OnLoadingScreenEnabled;
            _activePings.Clear();
            if (_overlayHost != null)
            {
                UnityEngine.Object.Destroy(_overlayHost);
                _overlayHost = null;
            }
        }

        public bool ProcessInput()
        {
            PruneExpired();

            if (!_inputService.IsKeyDown(PingKeyBindingId)) return false;
            if (EventIO.IsNull) return false;
            if (_inputService.MouseOverUI) return false;


            PingEvent pingEvent = CreatePingEventAndPing();
            // Use DoPrefix to only send events when appropriate
            ReplayEvent.DoPrefix(() =>
            {
                return pingEvent;
            });

            return false;
        }

        private PingEvent CreatePingEventAndPing()
        {
            Vector2 mouseScreen = _inputService.MousePosition;
            Ray gridRay = _cameraService.ScreenPointToRayInGridSpace(mouseScreen);

            Vector3 worldPos;
            var hit = _terrainPicker.PickTerrainCoordinates(gridRay);
            if (hit.HasValue)
            {
                worldPos = CoordinateSystem.GridToWorld(hit.Value.Intersection);
            }
            else
            {
                // Fall back to the y=0 plane so off-map clicks still register.
                Ray worldRay = _cameraService.ScreenPointToRayInWorldSpace(mouseScreen);
                if (worldRay.direction.y >= -0.0001f) return null;
                float t = -worldRay.origin.y / worldRay.direction.y;
                worldPos = worldRay.origin + worldRay.direction * t;
            }

            string senderName = Settings.PingDisplayName;
            Color color = Settings.PingColorValue;

            // Play the ping immediately locally, since it looks better not
            // to have any delay
            AddPing(worldPos, senderName, color);

            return new PingEvent
            {
                worldX = worldPos.x,
                worldY = worldPos.y,
                worldZ = worldPos.z,
                senderName = senderName,
                colorHex = ColorUtility.ToHtmlStringRGB(color),
            };
        }

        public void AddPing(Vector3 worldPosition, string senderName, Color color)
        {
            float now = Time.unscaledTime;
            _activePings.Add(new ActivePing
            {
                WorldPosition = worldPosition,
                SenderName = senderName ?? string.Empty,
                Color = color,
                CreatedTime = now,
                ExpiryTime = now + PingDurationSeconds,
            });
        }

        private void PruneExpired()
        {
            float now = Time.unscaledTime;
            for (int i = _activePings.Count - 1; i >= 0; i--)
            {
                if (_activePings[i].ExpiryTime <= now)
                {
                    _activePings.RemoveAt(i);
                }
            }
        }
    }
}
