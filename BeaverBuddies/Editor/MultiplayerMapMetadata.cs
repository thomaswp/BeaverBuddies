using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Timberborn.MapMetadataSystem;
using Timberborn.MapRepositorySystem;

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

    public class MultiplayerMapMetadataService : RegisteredSingleton
    {
        private readonly MapDeserializer _mapDeserializer;
        private readonly MapMetadataSerializer _mapMetadataSerializer;

        public MultiplayerMapMetadataService(MapDeserializer mapDeserializer, MapMetadataSerializer mapMetadataSerializer)
        {
            _mapDeserializer = mapDeserializer;
            _mapMetadataSerializer = mapMetadataSerializer;
        }

        public MultiplayerMapMetadata TryGetMultiplayerMapMetadata(MapFileReference reference)
        {
            // If it doesn't have multiplayer data, it returns null
            return _mapDeserializer.ReadFromMapFile(reference, _mapMetadataSerializer) as MultiplayerMapMetadata;
        }
    }
}
