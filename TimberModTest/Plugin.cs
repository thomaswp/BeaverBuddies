using BepInEx;
using BepInEx.Logging;
using Bindito.Core;
using HarmonyLib;
using Newtonsoft.Json;
using System.Diagnostics;
using TimberApi.ConfiguratorSystem;
using TimberApi.ConsoleSystem;
using TimberApi.ModSystem;
using TimberApi.SceneSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Coordinates;
using Timberborn.GameDistricts;
using Timberborn.Metrics;
using Timberborn.PrefabSystem;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Tilemaps.Tilemap;

namespace TimberModTest
{

    [Configurator(SceneEntrypoint.MainMenu)]  // This attribute registers the configurator and tells where it should be loaded
    public class MenuConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            //new ReplayService(null);
            //ReplayService.RecordEvent(new BuildingPlacedEvent()
            //{
            //    prefab = "test",
            //    coordinates = new Vector3Int(1, 2, 3),
            //    orientation = Timberborn.Coordinates.Orientation.Cw180,
            //});
        }
    }

    [Configurator(SceneEntrypoint.InGame)]  // This attribute registers the configurator and tells where it should be loaded
    public class ReplayConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            Plugin.Log($"Registering In Game Services");
            containerDefinition.Bind<DeterminismService>().AsSingleton();
            //containerDefinition.Bind<TickWathcerService>().AsSingleton();
            containerDefinition.Bind<ReplayService>().AsSingleton();
            containerDefinition.Bind<ServerConnectionService>().AsSingleton();
        }
    }

    [Configurator(SceneEntrypoint.MainMenu)]
    public class ConnectionMenuConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            Plugin.Log($"Registering Main Menu Services");
            containerDefinition.Bind<ClientConnectionService>().AsSingleton();
        }
    }

    [HarmonyPatch]
    public class Plugin : IModEntrypoint
    {
        private static IConsoleWriter logger;

        public const string PluginGuid = PluginInfo.PLUGIN_GUID;

        public void Entry(IMod mod, IConsoleWriter consoleWriter)
        {
            logger = consoleWriter;
            Log($"Plugin {PluginGuid} is loaded!");
            new Harmony(PluginGuid).PatchAll();
        }

        public static void Log(string message)
        {
            logger.LogWarning(message);
        }

        public static void LogStackTrace()
        {
            Log(new StackTrace().ToString());
        }
    }
}
