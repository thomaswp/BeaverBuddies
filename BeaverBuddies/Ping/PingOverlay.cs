using System.Collections.Generic;
using Timberborn.CameraSystem;
using UnityEngine;

namespace BeaverBuddies.Ping
{
    public class PingOverlay : MonoBehaviour
    {
        public PingService Service;
        public CameraService CameraService;

        private const float MarkerRadius = 18f;
        private const float MarkerThickness = 3f;
        private const float ArrowEdgeMargin = 60f;
        private const float ArrowSize = 28f;
        private const float NameOffset = 24f;

        private static Texture2D _whiteTex;
        private GUIStyle _labelStyle;

        private static Texture2D WhiteTex
        {
            get
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                }
                return _whiteTex;
            }
        }

        public void OnGUI()
        {
            if (Service == null) return;

            IReadOnlyList<ActivePing> pings = Service.ActivePings;
            if (pings.Count == 0) return;

            Camera cam = TryGetCamera();
            if (cam == null || !cam.isActiveAndEnabled) return;

            EnsureLabelStyle();

            float screenW = Screen.width;
            float screenH = Screen.height;
            Vector2 center = new Vector2(screenW * 0.5f, screenH * 0.5f);
            float now = Time.unscaledTime;

            foreach (var ping in pings)
            {
                float life = Mathf.InverseLerp(ping.ExpiryTime, ping.CreatedTime, now);
                Color color = ping.Color;
                color.a = Mathf.Clamp01(life);

                Vector3 sp = cam.WorldToScreenPoint(ping.WorldPosition);
                bool behind = sp.z < 0f;
                Vector2 guiPos = new Vector2(sp.x, screenH - sp.y);

                bool offScreen = behind
                    || guiPos.x < ArrowEdgeMargin
                    || guiPos.x > screenW - ArrowEdgeMargin
                    || guiPos.y < ArrowEdgeMargin
                    || guiPos.y > screenH - ArrowEdgeMargin;

                if (offScreen)
                {
                    Vector2 dir = guiPos - center;
                    if (behind) dir = -dir;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                    dir.Normalize();

                    Vector2 edgePos = ClampToEdge(center, dir, screenW, screenH, ArrowEdgeMargin);
                    DrawArrow(edgePos, dir, ArrowSize, color);
                    DrawLabel(edgePos + new Vector2(0, NameOffset), ping.SenderName, color);
                }
                else
                {
                    float pulse = 1f + 0.25f * Mathf.Sin((now - ping.CreatedTime) * 10f);
                    DrawCircleOutline(guiPos, MarkerRadius * pulse, MarkerThickness, color);
                    DrawLabel(guiPos + new Vector2(0, NameOffset + MarkerRadius), ping.SenderName, color);
                }
            }
        }

        private void EnsureLabelStyle()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
            }
        }

        private Camera TryGetCamera()
        {
            if (CameraService == null) return null;
            var t = CameraService.Transform;
            if (t == null || (Object)t == null) return null;
            return t.GetComponent<Camera>();
        }

        private static Vector2 ClampToEdge(Vector2 center, Vector2 dir, float w, float h, float margin)
        {
            float tx = float.PositiveInfinity;
            float ty = float.PositiveInfinity;
            if (Mathf.Abs(dir.x) > 0.0001f)
            {
                float boundX = dir.x > 0 ? (w - margin) : margin;
                tx = (boundX - center.x) / dir.x;
            }
            if (Mathf.Abs(dir.y) > 0.0001f)
            {
                float boundY = dir.y > 0 ? (h - margin) : margin;
                ty = (boundY - center.y) / dir.y;
            }
            return center + dir * Mathf.Min(tx, ty);
        }

        private static void DrawCircleOutline(Vector2 center, float radius, float thickness, Color color)
        {
            const int segments = 28;
            float step = (Mathf.PI * 2f) / segments;
            Vector2 prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = step * i;
                Vector2 next = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                DrawLine(prev, next, thickness, color);
                prev = next;
            }
        }

        private static void DrawArrow(Vector2 tip, Vector2 dir, float size, Color color)
        {
            Vector2 back = -dir * size;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (size * 0.5f);
            Vector2 b = tip + back + perp;
            Vector2 c = tip + back - perp;
            DrawLine(tip, b, 3f, color);
            DrawLine(tip, c, 3f, color);
            DrawLine(b, c, 3f, color);
        }

        private static void DrawLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.001f) return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), WhiteTex);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawLabel(Vector2 pos, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;

            _labelStyle.normal.textColor = new Color(0, 0, 0, color.a * 0.6f);
            GUI.Label(new Rect(pos.x - 100 + 1, pos.y + 1, 200, 20), text, _labelStyle);

            _labelStyle.normal.textColor = color;
            GUI.Label(new Rect(pos.x - 100, pos.y, 200, 20), text, _labelStyle);
        }
    }
}
