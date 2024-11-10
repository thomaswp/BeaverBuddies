using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Timberborn.MapMetadataSystem;

namespace BeaverBuddies.Editor
{
    public class MultiplayerMapMetadata : MapMetadata
    {
        public int MaxPlayers { get; }

        public MultiplayerMapMetadata(MapMetadata metadata, int maxPlayers) : 
            base(metadata.Width, metadata.Height, metadata.MapNameLocKey, metadata.MapDescriptionLocKey, metadata.MapDescription, metadata.IsRecommended, metadata.IsDev)
        {
            MaxPlayers = maxPlayers;
        }
    }
}
