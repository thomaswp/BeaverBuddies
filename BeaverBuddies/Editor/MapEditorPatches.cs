using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectModelSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BottomBarSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.MapEditorUI;
using Timberborn.MapMetadataSystem;
using Timberborn.MapMetadataSystemUI;
using Timberborn.MapThumbnailCapturing;
using Timberborn.SerializationSystem;
using Timberborn.StartingLocationSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace BeaverBuddies.Editor
{

    [HarmonyPatch(typeof(MapEditorButtons), nameof(MapEditorButtons.GetElements))]
    public class MapEditorButtonsGetElementsPatcher
    {

        public static void Postfix(MapEditorButtons __instance, ref IEnumerable<BottomBarElement> __result)
        {
            ToolGroupSpec toolGroupSpecification = __instance._toolGroupSpecService.GetToolGroupSpecs(MapEditorButtons.MapEditorToolGroup);
            IEnumerable<PlaceableBlockObjectSpec> blockObjectsFromGroup = __instance._blockObjectToolGroupSpecService.GetBlockObjectsFromGroup(toolGroupSpecification);
            Plugin.Log("Buttons:");
            PlaceableBlockObjectSpec startingLocation = null;
            foreach (PlaceableBlockObjectSpec item in blockObjectsFromGroup)
            {
                if (item.name == "StartingLocation")
                {
                    startingLocation = item;
                    ToolButton toolButton = __instance._blockObjectToolButtonFactory.Create(item);

                    break;
                }
            }
            if (startingLocation == null)
            {
                Plugin.LogError("StartingLocation not found");
                return;
            }

            List<BottomBarElement> newResult = new List<BottomBarElement>(__result);

            List<PlaceableBlockObjectSpec> startingLocations = new List<PlaceableBlockObjectSpec>();
            Sprite baseSprite = __instance._blockObjectToolButtonFactory.GetToolImage(startingLocation);
            for (int i = 0; i < StartingLocationPlayer.MAX_PLAYERS; i++)
            {
                // Duplicate the block object
                var playerStartingLocation = UnityEngine.Object.Instantiate(startingLocation);
                playerStartingLocation.GameObjectFast.AddComponent<StartingLocationPlayer>().PlayerIndex = i;
                playerStartingLocation.GetComponentFast<LabeledEntitySpec>()._displayNameLocKey = $"BeaverBuddies.Editor.StartingLocation{i + 1}";
                startingLocations.Add(playerStartingLocation);

                // To update the icons, I would need to create actual media, since they're
                // loaded from a path, given in the LabeledEntitySpec
            }

            ToolGroupSpec spec = new ToolGroupSpec() {
                Id = null,
                Order = 0,
                NameLocKey = startingLocation.GetComponentFast<LabeledEntitySpec>()._displayNameLocKey, // TODO: Find loc key, or add it
                Icon = baseSprite,
                FallbackGroup = false
            };
            var startingLocsToolGroup = __instance._blockObjectToolGroupFactory.Create(spec, startingLocations);

            // TODO: Find it in the original list somehow
            newResult[7] = startingLocsToolGroup;

            __result = newResult;
        }

        private static Sprite TintSprite(Sprite sprite, Color tintColor)
        {
            Texture2D texture = sprite.texture;

            // Create a new Texture2D to store the tinted version
            Texture2D tintedTexture = new Texture2D(texture.width, texture.height);

            // Loop through all pixels of the texture and apply the tint
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    // Get the color of the pixel and apply the tint
                    Color pixelColor = texture.GetPixel(x, y);
                    tintedTexture.SetPixel(x, y, pixelColor * tintColor); // Multiply the pixel color by the tint
                }
            }

            // Apply the changes to the tinted texture
            tintedTexture.Apply();

            // Create a new sprite from the tinted texture
            Sprite tintedSprite = Sprite.Create(tintedTexture, sprite.rect, sprite.pivot);

            return tintedSprite;
        }
    }

    [ManualMethodOverwrite]
    /*
     * 04/19/2025
	private void DeleteOtherStartingLocations(StartingLocation remainingStartingLocation)
	{
		foreach (StartingLocation item in (from startingLocation in _entityComponentRegistry.GetEnabled<StartingLocationSpec>()
			where startingLocation != remainingStartingLocationSpec
			select startingLocation).ToList())
		{
			_entityService.Delete(item);
		}
	}
     */
    [HarmonyPatch(typeof(StartingLocationService), nameof(StartingLocationService.DeleteOtherStartingLocations))]
    public class StartingLocationServiceDeleteOtherStartingLocationsPatcher
    {
        public static bool Prefix(StartingLocationService __instance, StartingLocationSpec remainingStartingLocation)
        {
            var player = remainingStartingLocation.GetComponentFast<StartingLocationPlayer>();
            if (!player)
            {
                return true;
            }
            Plugin.Log($"Deleting other starting locations for {player.PlayerIndex}");
            foreach (StartingLocationPlayer item in (from startingLocation in __instance._entityComponentRegistry.GetEnabled<StartingLocationPlayer>()
                                               // Get only the starting locations that match this player index
                                               where startingLocation != player && startingLocation.PlayerIndex == player.PlayerIndex
                                               select startingLocation).ToList())
            {
                __instance._entityService.Delete(item);
            }
            return false;
        }
    }

    [ManualMethodOverwrite]
    /*
     * 04/19/2025
	List<StartingLocation> list = _entityComponentRegistry.GetEnabled<StartingLocation>().ToList();
	if (list.IsEmpty())
	{
		throw new InvalidOperationException("No StartingLocation exists.");
	}
	if (list.Count > 1)
	{
		throw new InvalidOperationException("There must be only one StartingLocation.");
	}
	return list[0];
     */
    [HarmonyPatch(typeof(StartingLocationService), nameof(StartingLocationService.GetStartingLocation))]
    public class StartingLocationServiceGetStartingLocationPatcher
    {
        public static bool Prefix(StartingLocationService __instance, ref StartingLocationSpec __result)
        {
            List<StartingLocationSpec> list = __instance._entityComponentRegistry.GetEnabled<StartingLocationSpec>().ToList();
            if (list.Count <= 1)
            {
                return true;
            }
            Plugin.LogWarning("Requesting single starting location; returning first item");
            Plugin.LogStackTrace();
            __result = list[0];
            return false;
        }
    }


    [HarmonyPatch(typeof(MapMetadataSaveEntryWriter), nameof(MapMetadataSaveEntryWriter.CreateMapMetadata))]
    public class MapMetadataSaveEntryWriterCreateMapMetadataPatcher
    {
        public static void Postfix(MapMetadataSaveEntryWriter __instance, ref MapMetadata __result)
        {
            var startingLocNumberService = SingletonManager.GetSingleton<StartingLocationNumberService>();
            startingLocNumberService.ResetNumbering();
            int maxPlayers = startingLocNumberService.GetMaxPlayers();
            Plugin.Log($"Saving map with max players: {maxPlayers}");
            __result = new MultiplayerMapMetadata(__result, maxPlayers);
        }
    }

    [HarmonyPatch(typeof(MapMetadataSerializer), nameof(MapMetadataSerializer.GetMapMetadataSerializedObject))]
    public class MapMetadataSerializerGetMapMetadataSerializedObjectPatcher
    {
        public static readonly string MaxPlayersKey = "MaxPlayers";

        public static void Postfix(MapMetadata mapMetadata, ref SerializedObject __result)
        {
            if (mapMetadata is MultiplayerMapMetadata)
            {
                __result.Set(MaxPlayersKey, (mapMetadata as MultiplayerMapMetadata).MaxPlayers);
            }
        }
    }

    [HarmonyPatch(typeof(MapMetadataSerializer), nameof(MapMetadataSerializer.Deserialize))]
    public class MapMetadataSerializerDeserializePatcher
    {
        public static void Postfix(SerializedObject serializedObject, ref MapMetadata __result)
        {
            string key = MapMetadataSerializerGetMapMetadataSerializedObjectPatcher.MaxPlayersKey;
            if (serializedObject.Has(key))
            {
                __result = new MultiplayerMapMetadata(__result, serializedObject.Get<int>(key));
            }
        }
    }

    // Include starting locations in the renderer, since they're important
    [HarmonyPatch(typeof(StartingLocationThumbnailRenderingListener), nameof(StartingLocationThumbnailRenderingListener.PreThumbnailRendering))]
    public class StartingLocationThumbnailRenderingListenerPreThumnailRenderingPatcher
    {
        public static bool Prefix()
        {
            return false;
        }
    }
}
