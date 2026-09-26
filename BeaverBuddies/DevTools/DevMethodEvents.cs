using BeaverBuddies.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Debugging;
using Timberborn.DebuggingUI;
using Timberborn.MortalSystem;

namespace BeaverBuddies.DevTools
{
    public enum DevMethodSync
    {
        // Only affects this player (visuals, camera, diagnostics)
        Local,
        // Changes the game through code that is already synced elsewhere
        AlreadySynced,
        // Changes the game, so it's replayed by name for every player
        Replayed,
        // Changes the game in a way we can't sync, or we don't know it
        Blocked,
    }

    public static class DevMethodClassifier
    {
        private static readonly HashSet<string> Replayed = new()
        {
            "Add 1000 Science",
            "Fill input inventories",
            "Kill all characters instantly",
            "Jump to next daytime",
            "Jump to next season",
            "Water simulation: Reset all",
            "Toggle long lasting corpses",
            "Toggle forced wind",
            "Water wheels: increase max speed",
            "Water wheels: decrease max speed",
        };

        private static readonly HashSet<string> AlreadySynced = new()
        {
            // Speed changes go through SpeedManager.ChangeSpeed
            "Speed: x0.25",
            "Speed: x30",
            "Speed: x99",
            // CharacterKillerPatches record these with the selected character
            "Kill selected character",
            "Kill all characters except selected",
        };

        private static readonly HashSet<string> Local = new()
        {
            "Toggle deconstruction tool preview",
            "Reset debugging panels position",
            "Dump mesh metrics",
            "Toggle GC",
            "Trigger GC",
            "Load empty scene",
            "Toggle sound listener debugger",
            "Toggle status slots",
            "Toggle zipline cable blocks",
            "Toggle nav mesh",
            "Auto move levels up",
            "Auto move levels down",
            "Sky: Toggle fog",
            "Highlight resource reproduction spots",
            "Automation: Log partitions",
            "Force water on",
            // Wonder completions are saved to this player's profile
            "Wonders: Complete on this map",
            "Wonders: Revoke all completions",
        };

        private static readonly string[] LocalPrefixes =
        {
            "Toggle models: ",
            "Toggle water logic: ",
            "Camera",
            "UI Scale: ",
            "Stopwatch: ",
            "Test ",
            "Multithreading: ",
        };

        public static DevMethodSync Classify(string name)
        {
            if (Replayed.Contains(name)) return DevMethodSync.Replayed;
            // "Kill 10% of characters", the number comes from a constant
            if (name.StartsWith("Kill ") && name.EndsWith("% of characters")) return DevMethodSync.Replayed;
            if (AlreadySynced.Contains(name)) return DevMethodSync.AlreadySynced;
            if (Local.Contains(name)) return DevMethodSync.Local;
            if (LocalPrefixes.Any(name.StartsWith)) return DevMethodSync.Local;
            // "Save 3x to memory"
            if (name.StartsWith("Save ") && name.EndsWith(" to memory")) return DevMethodSync.Local;
            return DevMethodSync.Blocked;
        }
    }

    [Serializable]
    public class DevMethodEvent : DevToolEvent
    {
        public string methodName;

        public override void Replay(IReplayContext context)
        {
            var devPanel = context.GetSingleton<DevPanel>();
            DevMethod method = devPanel?._devMethods?.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                Plugin.LogWarning($"Could not find dev method: {methodName}");
                return;
            }
            method.Invoke();
        }

        public override string ToActionString()
        {
            return $"Dev method: {methodName}";
        }
    }

    // Covers both the dev panel buttons and their key bindings
    [HarmonyPatch(typeof(DevMethod), nameof(DevMethod.Invoke))]
    class DevMethodInvokePatcher
    {
        static bool Prefix(DevMethod __instance)
        {
            if (!DevToolsPolicy.IsRestricted) return true;

            string name = __instance.Name;
            switch (DevMethodClassifier.Classify(name))
            {
                case DevMethodSync.Local:
                    return true;
                case DevMethodSync.AlreadySynced:
                    return DevToolsPolicy.CheckAllowed();
                case DevMethodSync.Replayed:
                    return DevToolsPolicy.DoPrefix(() => new DevMethodEvent() { methodName = name });
                default:
                    Plugin.Log($"Blocking dev method in co-op: {name}");
                    return DevToolsPolicy.Block();
            }
        }
    }

    [Serializable]
    public class DevKillCharactersEvent : DevToolEvent
    {
        public string selectedID;
        public bool allExceptSelected;
        public bool dieInstantly;

        public override void Replay(IReplayContext context)
        {
            var killer = context.GetSingleton<CharacterKiller>();
            var selected = GetComponent<Mortal>(context, selectedID);
            if (killer == null || selected == null) return;

            // The killer acts on its selected character, which is local UI
            // state, so point it at the recorded one while replaying
            Mortal previous = killer._selectedMortal;
            killer._selectedMortal = selected;
            try
            {
                if (allExceptSelected)
                {
                    killer.KillAllExceptSelected();
                }
                else
                {
                    killer.KillSelectedCharacter(dieInstantly);
                }
            }
            finally
            {
                killer._selectedMortal = previous;
            }
        }

        public override string ToActionString()
        {
            return allExceptSelected
                ? $"Dev: killing all characters except {selectedID}"
                : $"Dev: killing character {selectedID}";
        }
    }

    [HarmonyPatch(typeof(CharacterKiller), nameof(CharacterKiller.KillSelectedCharacter))]
    class CharacterKillerKillSelectedPatcher
    {
        static bool Prefix(CharacterKiller __instance, bool dieInstantly)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                string id = ReplayEvent.GetEntityID(__instance._selectedMortal);
                if (id == null) return null;
                return new DevKillCharactersEvent() { selectedID = id, dieInstantly = dieInstantly };
            });
        }
    }

    [HarmonyPatch(typeof(CharacterKiller), nameof(CharacterKiller.KillAllExceptSelected))]
    class CharacterKillerKillAllExceptSelectedPatcher
    {
        static bool Prefix(CharacterKiller __instance)
        {
            return DevToolsPolicy.DoPrefix(() =>
            {
                string id = ReplayEvent.GetEntityID(__instance._selectedMortal);
                if (id == null) return null;
                return new DevKillCharactersEvent() { selectedID = id, allExceptSelected = true };
            });
        }
    }

    // Dev mode itself can't be turned on when the host disallows dev tools
    [HarmonyPatch(typeof(DevModeManager), nameof(DevModeManager.EnableSilently))]
    class DevModeManagerEnablePatcher
    {
        static bool Prefix()
        {
            if (ReplayEvent.GetReplayServiceIfReady() == null) return true;
            return DevToolsPolicy.CheckAllowed();
        }
    }
}
