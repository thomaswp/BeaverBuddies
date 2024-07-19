using BepInEx;
using BepInEx.Logging;
using System;
using Timberborn.ModManagerScene;

namespace Mods.HelloWorld.Scripts
{
    internal class HelloWorldLogger : IModStarter
    {

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void StartMod()
        {
            var playerLogPath = UnityEngine.Application.persistentDataPath + "/Player.log";
            UnityEngine.Debug.Log("Hello World, but in the Player.log file at: " + playerLogPath);

            var source = Logger.CreateLogSource("test");
            source.LogWarning("Warning");
            source.LogWarning("Error");
            source.LogInfo("Info!");


        }

    }
}