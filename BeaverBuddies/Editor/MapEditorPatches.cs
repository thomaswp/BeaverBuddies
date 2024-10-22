using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BottomBarSystem;
using Timberborn.Common;
using Timberborn.MapEditorUI;
using Timberborn.StartingLocationSystem;
using Timberborn.ToolSystem;

namespace BeaverBuddies.Editor
{

    [HarmonyPatch(typeof(MapEditorButtons), nameof(MapEditorButtons.GetElements))]
    public class MapEditorButtonsGetElementsPatcher
    {
        public const int MAX_STARTING_LOCATIONS = 4;

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
            for (int i = 0; i < MAX_STARTING_LOCATIONS; i++)
            {
                startingLocations.Add(startingLocation);
            }

            ToolGroupSpecification spec = new ToolGroupSpecification(
                null,
                0,
                startingLocation.name, // TODO: Find loc key, or add it
                __instance._blockObjectToolButtonFactory.GetToolImage(startingLocation),
                false
            );
            var startingLocsToolGroup = __instance._blockObjectToolGroupFactory.Create(spec, startingLocations);

            // TODO: Find it in the original list somehow
            newResult[7] = startingLocsToolGroup;

            __result = newResult;
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
}
