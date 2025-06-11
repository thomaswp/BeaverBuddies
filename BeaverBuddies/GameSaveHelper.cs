using HarmonyLib;
using System;
using System.IO;
using Timberborn.CameraSystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameScene;
using TimberNet;

namespace BeaverBuddies
{
    internal class GameSaveHelper
    {
        public GameSaver _gameSaver;

        public static bool IsSavingDeterministically { get; private set; } = false;

        public GameSaveHelper(GameSaver gameSaver)
        {
            _gameSaver = gameSaver;
        }
    }

    [HarmonyPatch(typeof(DateSalter), nameof(DateSalter.Save))]
    static class DateSatlerSavePatcher
    {
        static bool Prefix()
        {
            return !GameSaveHelper.IsSavingDeterministically;
        }
    }

    [HarmonyPatch(typeof(DateTime), nameof(DateTime.ToString), typeof(string))]
    static class DateTimeStringPatcher
    {
        static bool Prefix(ref string __result)
        {
            if (!GameSaveHelper.IsSavingDeterministically) return true;
            __result = "";
            return false;
        }
    }
}
