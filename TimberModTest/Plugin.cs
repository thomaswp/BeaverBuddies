using BepInEx;
using BepInEx.Logging;
using Bindito.Core;
using HarmonyLib;
using Newtonsoft.Json;
using TimberApi.ConfiguratorSystem;
using TimberApi.ConsoleSystem;
using TimberApi.ModSystem;
using TimberApi.SceneSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.Coordinates;
using Timberborn.GameDistricts;
using Timberborn.Metrics;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WalkingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberModTest
{
    //[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    //public class Plugin : BaseUnityPlugin
    //{
    //    private static Plugin I;

    //    private void Awake()
    //    {
    //        // Plugin startup logic
    //        I = this;
    //        Log($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    //    }

    //    public static void Log(string message)
    //    {
    //        I.Logger.LogWarning(message);
    //    }
    //}

    [Configurator(SceneEntrypoint.InGame)]  // This attribute registers the configurator and tells where it should be loaded
    public class ExampleConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            Plugin.Log($"Registering TestService");
            containerDefinition.Bind<TestService>().AsSingleton();
            containerDefinition.Bind<TickWathcerService>().AsSingleton();
            containerDefinition.Bind<ReplayService>().AsSingleton();
        }
    }

    [Configurator(SceneEntrypoint.MainMenu)]
    public class MainMenuConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            Plugin.Log("Creating info service");
            containerDefinition.Bind<InfoService>().AsSingleton();
        }
    }

    public class InfoService
    {
        InfoService(EventBus eventBus, IRandomNumberGenerator gen)
        {
            //Plugin.Log("All MM events:");
            //foreach (var key in eventBus._subscriptions._subscriptions.Keys)
            //{
            //    Plugin.Log($"EventBus subscription: {key.Name}");
            //}
        }
    }

    

    public class TestService
    {
        EventBus _eventBus;

        TestService(EventBus eventBus, IRandomNumberGenerator gen)
        {
            _eventBus = eventBus;
            Plugin.Log($"Creating test service {eventBus}");
            eventBus.Register(this);
            UnityEngine.Random.InitState(1234);
            Plugin.Log($"Hopefully deterministic random number {gen.Range(0, 100)}");
        }

        [OnEvent]
        public void OnConstructionEvent(ConstructibleEnteredUnfinishedStateEvent e)
        {
            //Plugin.Log($"Const entered unfinished state {e.Constructible.name}");
        }

        [OnEvent]
        public void OnSpeedEvent(CurrentSpeedChangedEvent e)
        {
            Plugin.Log($"Speed changed to: {e.CurrentSpeed}; random reset");
            UnityEngine.Random.InitState(1234);
            //Plugin.Log("All Game events:");
            //foreach (var key in _eventBus._subscriptions._subscriptions.Keys)
            //{
            //    Plugin.Log($"EventBus subscription: {key.Name}");
            //}
        }

        //[OnEvent]
        //public void OnStartEvent(Event e)
        //{
        //    Plugin.Log($"Speed changed to: {e.CurrentSpeed}; random reset");
        //    UnityEngine.Random.InitState(1234);
        //}
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

            string json = JsonConvert.SerializeObject(new BuildingPlacedEvent()
            {
                prefab = "test",
                coordinates = new Vector3Int(1, 2, 3),
                orientation = Orientation.Cw180,
            });
            Log("JSON!");
            Log(json);
            BuildingPlacedEvent e = JsonConvert.DeserializeObject<BuildingPlacedEvent>(json);
            Log("" + e.coordinates.y);
            Log("" + e.orientation);
        }

        public static void Log(string message)
        {
            logger.LogWarning(message);
        }
    }


}
