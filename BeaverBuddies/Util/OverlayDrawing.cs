using UnityEngine;

namespace BeaverBuddies.Util
{
    /**
     * Immediate mode GUI helpers shared by the overlays drawn on top
     * of the game (pings, player cursors).
     */
    public static class OverlayDrawing
    {
        private static Texture2D _whiteTex;

        public static Texture2D WhiteTex
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

        public static Camera TryGetCamera(Timberborn.CameraSystem.CameraService cameraService)
        {
            if (cameraService == null) return null;
            var t = cameraService.Transform;
            if (t == null || (Object)t == null) return null;
            return t.GetComponent<Camera>();
        }

        public static void DrawLine(Vector2 a, Vector2 b, float thickness, Color color)
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

        public static void DrawShadowedLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;

            style.normal.textColor = new Color(0, 0, 0, color.a * 0.6f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);

            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }
    }
}
