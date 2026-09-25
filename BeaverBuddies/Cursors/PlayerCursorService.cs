using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Timberborn.CameraSystem;
using Timberborn.InputSystem;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using Timberborn.TerrainQueryingSystem;
using UnityEngine;

namespace BeaverBuddies.Cursors
{
    public class RemoteCursor
    {
        public Vector3 TargetPosition;
        public Vector3 DisplayedPosition;
        public Color Color;
        public bool Visible;
        public float LastMessageTime;
    }

    /**
     * Shares the local mouse position with the other players and keeps
     * track of theirs. Positions go through transient messages, so they
     * don't wait for ticks, still move while paused and never touch the
     * game state.
     */
    public class PlayerCursorService : IPostLoadableSingleton, IUpdatableSingleton
    {
        public const string MessageType = "PlayerCursor";

        // Roughly 10 updates per second is enough once positions are smoothed
        private const float SendIntervalSeconds = 0.1f;
        // Resend an unchanged position now and then so a still cursor
        // doesn't look like a player that left
        private const float KeepAliveSeconds = 2f;
        private const float StaleSeconds = 5f;
        private const float MinMoveDistance = 0.05f;
        private const float SmoothingSpeed = 15f;

        private readonly InputService _inputService;
        private readonly CameraService _cameraService;
        private readonly TerrainPicker _terrainPicker;
        private readonly LoadingScreen _loadingScreen;

        private readonly Dictionary<string, RemoteCursor> _cursors = new();

        private GameObject _overlayHost;
        private float _lastSendTime = float.NegativeInfinity;
        private bool _lastSentVisible;
        private Vector3 _lastSentPosition;

        public PlayerCursorService(
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

        public IEnumerable<RemoteCursor> Cursors => _cursors.Values;

        public void PostLoad()
        {
            _overlayHost = new GameObject("BeaverBuddies_PlayerCursorOverlay");
            var overlay = _overlayHost.AddComponent<PlayerCursorOverlay>();
            overlay.Service = this;
            overlay.CameraService = _cameraService;

            EventIO.TransientMessageReceived += OnTransientMessageReceived;

            // Same as pings: clean up before the loading screen shows
            _loadingScreen.LoadingScreenEnabled += OnLoadingScreenEnabled;
        }

        private void OnLoadingScreenEnabled(object sender, EventArgs e)
        {
            _loadingScreen.LoadingScreenEnabled -= OnLoadingScreenEnabled;
            EventIO.TransientMessageReceived -= OnTransientMessageReceived;
            _cursors.Clear();
            if (_overlayHost != null)
            {
                UnityEngine.Object.Destroy(_overlayHost);
                _overlayHost = null;
            }
        }

        public void UpdateSingleton()
        {
            if (_overlayHost == null) return;

            float now = Time.unscaledTime;
            UpdateRemoteCursors(now);
            SendLocalCursor(now);
        }

        private void UpdateRemoteCursors(float now)
        {
            List<string> stale = null;
            float t = 1f - Mathf.Exp(-SmoothingSpeed * Time.unscaledDeltaTime);
            foreach (var pair in _cursors)
            {
                RemoteCursor cursor = pair.Value;
                if (now - cursor.LastMessageTime > StaleSeconds)
                {
                    (stale ??= new()).Add(pair.Key);
                    continue;
                }
                cursor.DisplayedPosition = Vector3.Lerp(cursor.DisplayedPosition, cursor.TargetPosition, t);
            }
            stale?.ForEach(id => _cursors.Remove(id));
        }

        private void SendLocalCursor(float now)
        {
            EventIO io = EventIO.Get();
            if (io == null) return;
            if (now - _lastSendTime < SendIntervalSeconds) return;

            Vector3 position = default;
            bool visible = Settings.ShowCursors
                && !_inputService.MouseOverUI
                && WorldPointPicker.TryPick(_cameraService, _terrainPicker, _inputService.MousePosition, out position);

            bool changed = visible != _lastSentVisible
                || (visible && Vector3.Distance(position, _lastSentPosition) > MinMoveDistance);
            bool keepAlive = visible && now - _lastSendTime > KeepAliveSeconds;
            if (!changed && !keepAlive) return;

            Color color = Settings.PingColorValue;
            io.SendTransientMessage(new JObject
            {
                [TimberNet.TimberNetBase.TYPE_KEY] = MessageType,
                ["playerID"] = ReplayEvent.LocalPlayerID,
                ["color"] = ColorUtility.ToHtmlStringRGB(color),
                ["visible"] = visible,
                ["x"] = position.x,
                ["y"] = position.y,
                ["z"] = position.z,
            });

            _lastSendTime = now;
            _lastSentVisible = visible;
            _lastSentPosition = position;
        }

        private void OnTransientMessageReceived(JObject message)
        {
            if (message[TimberNet.TimberNetBase.TYPE_KEY]?.ToString() != MessageType) return;

            try
            {
                string playerID = message.Value<string>("playerID");
                if (string.IsNullOrEmpty(playerID) || playerID == ReplayEvent.LocalPlayerID) return;

                Vector3 position = new Vector3(
                    message.Value<float>("x"),
                    message.Value<float>("y"),
                    message.Value<float>("z"));

                if (!_cursors.TryGetValue(playerID, out RemoteCursor cursor))
                {
                    cursor = new RemoteCursor { DisplayedPosition = position };
                    _cursors[playerID] = cursor;
                }
                else if (!cursor.Visible)
                {
                    // Don't slide in from wherever the cursor was last seen
                    cursor.DisplayedPosition = position;
                }

                cursor.TargetPosition = position;
                cursor.Visible = message.Value<bool>("visible");
                cursor.Color = ParseColor(message.Value<string>("color"));
                cursor.LastMessageTime = Time.unscaledTime;
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"Invalid cursor message: {e.Message}");
            }
        }

        private static Color ParseColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex)
                && ColorUtility.TryParseHtmlString("#" + hex, out var parsed))
            {
                parsed.a = 1f;
                return parsed;
            }
            return Color.white;
        }
    }
}
