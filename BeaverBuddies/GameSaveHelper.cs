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

        //public void LogStateCheck(int ticksSinceLoad, bool write = false)
        //{
        //    // TODO: This doesn't work yet.
        //    // There is definitely a timestamp in the save, which is part of
        //    // the issue. Need to test by saving to a file and unzipping/comparing.
        //    // There may also be tiny amounts of nondeterminism somewhere
        //    // (possibly harmless, and possibly problematic).
        //    // And it may be there are small rounding errors on things like seconds.
        //    MemoryStream ms = new MemoryStream();
        //    IsSavingDeterministically = true;
        //    _gameSaver.Save(ms);
        //    IsSavingDeterministically = false;
        //    byte[] bytes = ms.ToArray();
        //    int hash = TimberNetBase.GetHashCode(bytes);
        //    Plugin.Log($"State Check [T{ticksSinceLoad}]: {hash.ToString("X8")}");

        //    if (write)
        //    {
        //        File.WriteAllBytes(ticksSinceLoad + ".save", bytes);
        //    }
        //}
    }


    // Removed because this is old code and I'm not currently using deterministic saves.
    //[HarmonyPatch(typeof(CameraComponent), nameof(CameraComponent.Save))]
    //static class CameraComponentSavePatcher
    //{
    //    static bool Prefix()
    //    {
    //        return !GameSaveHelper.IsSavingDeterministically;
    //    }
    //}

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
