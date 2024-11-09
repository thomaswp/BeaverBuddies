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
            ToolGroupSpecification toolGroupSpecification = __instance._toolGroupSpecificationService.GetToolGroupSpecification(MapEditorButtons.MapEditorToolGroup);
            IEnumerable<PlaceableBlockObject> blockObjectsFromGroup = __instance._blockObjectToolGroupSpecificationService.GetBlockObjectsFromGroup(toolGroupSpecification);
            Plugin.Log("Buttons:");
            PlaceableBlockObject startingLocation = null;
            foreach (PlaceableBlockObject item in blockObjectsFromGroup)
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

            List<PlaceableBlockObject> startingLocations = new List<PlaceableBlockObject>();
            Sprite baseSprite = __instance._blockObjectToolButtonFactory.GetToolImage(startingLocation);
            for (int i = 0; i < StartingLocationPlayer.MAX_PLAYERS; i++)
            {
                // Duplicate the block object
                var playerStartingLocation = UnityEngine.Object.Instantiate(startingLocation);
                playerStartingLocation.GameObjectFast.AddComponent<StartingLocationPlayer>().PlayerIndex = i;
                startingLocations.Add(playerStartingLocation);

                // TODO: Find a way to update the icons (they're created dynamically when the button is created)
                // Could override the method below
                //icons.Add(TintSprite(baseSprite, StartingLocationPlayer.PLAYER_COLORS[i]));
            }

            ToolGroupSpecification spec = new ToolGroupSpecification(
                null,
                0,
                startingLocation.name, // TODO: Find loc key, or add it
                baseSprite,
                false
            );
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


        //[ManualMethodOverwrite]
        //private static BottomBarElement CreateFromButton(BlockObjectToolGroupFactory factory, ToolGroupSpecification spec, IEnumerable<PlaceableBlockObject> blockObjects)
        //{
        //    BlockObjectToolGroup toolGroup = new BlockObjectToolGroup(spec);
        //    ToolGroupButton toolGroupButton = factory._toolGroupButtonFactory.CreateGreen(toolGroup);
        //    foreach (PlaceableBlockObject blockObject in blockObjects)
        //    {
        //        if (blockObject.UsableWithCurrentFeatureToggles)
        //        {
        //            ToolButton button = factory._blockObjectToolButtonFactory.Create(blockObject, toolGroup, toolGroupButton.ToolButtonsElement);
        //            toolGroupButton.AddTool(button);
        //        }
        //    }
        //    return BottomBarElement.CreateMultiLevel(toolGroupButton.Root, toolGroupButton.ToolButtonsElement);
        //}
    }

    [ManualMethodOverwrite]
    /*
     * 11/9/2024
	private void DeleteOtherStartingLocations(StartingLocation remainingStartingLocation)
	{
		foreach (StartingLocation item in (from startingLocation in _entityComponentRegistry.GetEnabled<StartingLocation>()
			where startingLocation != remainingStartingLocation
			select startingLocation).ToList())
		{
			_entityService.Delete(item);
		}
	}
     */
    [HarmonyPatch(typeof(StartingLocationService), nameof(StartingLocationService.DeleteOtherStartingLocations))]
    public class StartingLocationServiceDeleteOtherStartingLocationsPatcher
    {
        public static bool Prefix(StartingLocationService __instance, StartingLocation remainingStartingLocation)
        {
            var player = remainingStartingLocation.GetComponentFast<StartingLocationPlayer>();
            if (!player)
            {
                // TODO: Only show warning in Editor - normal in Game as the entity hasn't yet been fully loaded (I guess?)
                Plugin.LogWarning("Missing StartingLocationPlayer!");
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
     * 11/9/2024
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
        public static bool Prefix(StartingLocationService __instance, ref StartingLocation __result)
        {
            List<StartingLocation> list = __instance._entityComponentRegistry.GetEnabled<StartingLocation>().ToList();
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

    // TODO: Timberborn.MapThumbnailCapturing.StartingLocationThumbnailRenderingListener.PreThumbnailRendering

}
