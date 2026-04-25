using BeaverBuddies.Events;
using System;
using UnityEngine;

namespace BeaverBuddies.Ping
{
    [Serializable]
    public class PingEvent : ReplayEvent
    {
        public float worldX;
        public float worldY;
        public float worldZ;
        public string senderName;
        public string colorHex;

        [NonSerialized]
        public bool IsLocal = false;

        public Vector3 WorldPosition => new Vector3(worldX, worldY, worldZ);

        public override void Replay(IReplayContext context)
        {
            // If the ping came from this player, just ignore it,
            // since we've already added it locally when it was created.
            if (IsLocal) return;

            var pingService = context.GetSingleton<PingService>();
            if (pingService == null) return;

            Color color = Color.white;
            if (!string.IsNullOrEmpty(colorHex)
                && ColorUtility.TryParseHtmlString("#" + colorHex, out var parsed))
            {
                parsed.a = 1f;
                color = parsed;
            }
            pingService.AddPing(WorldPosition, senderName, color);
        }

        public override string ToActionString()
        {
            return $"Doing: Ping ({worldX:F1},{worldY:F1},{worldZ:F1}) from '{senderName}'";
        }
    }
}
