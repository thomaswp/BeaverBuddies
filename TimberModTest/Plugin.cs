using Bindito.Core;
using HarmonyLib;
using System.Diagnostics;
using TimberApi.ConfiguratorSystem;
using TimberApi.ConsoleSystem;
using TimberApi.ModSystem;
using TimberApi.SceneSystem;

namespace TimberModTest
{
    [Configurator(SceneEntrypoint.InGame)]  // This attribute registers the configurator and tells where it should be loaded
    public class ReplayConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            if (EventIO.Config.GetNetMode() == NetMode.None) return;

            // Reset everything before loading singletons
            SingletonManager.Reset();
            // Reset the event IO if we're not a client, since the
            // EventIO will have been set from the MainMenu
            // TODO: I think I need a more robust solution here, especially
            // when I start making a UI for this...
            if (EventIO.Config.GetNetMode() != NetMode.Client)
            {
                EventIO.Reset();
            }

            Plugin.Log($"Registering In Game Services");
            //containerDefinition.Bind<DeterminismService>().AsSingleton();
            containerDefinition.Bind<ReplayService>().AsSingleton();
            containerDefinition.Bind<ServerConnectionService>().AsSingleton();
            containerDefinition.Bind<RecordToFileService>().AsSingleton();
            containerDefinition.Bind<TickProgressService>().AsSingleton();
            containerDefinition.Bind<TickingService>().AsSingleton();
            containerDefinition.Bind<DeterminismService>().AsSingleton();
        }
    }

    [Configurator(SceneEntrypoint.MainMenu)]
    public class ConnectionMenuConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            if (EventIO.Config.GetNetMode() == NetMode.None) return;

            // This will be called if the player exits to the main menu,
            // so it's best to reset everything.
            SingletonManager.Reset();
            EventIO.Reset();

            Plugin.Log($"Registering Main Menu Services");
            containerDefinition.Bind<ClientConnectionService>().AsSingleton();

            //ReflectionUtils.PrintChildClasses(typeof(MonoBehaviour), 
            //    "Start", "Awake", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy");
            //ReflectionUtils.PrintChildClasses(typeof(IUpdatableSingleton));
            //ReflectionUtils.PrintChildClasses(typeof(ILateUpdatableSingleton));
            //ReflectionUtils.PrintChildClasses(typeof(IBatchControlRowItem));
            //ReflectionUtils.PrintChildClasses(typeof(IUpdateableBatchControlRowItem));
            //ReflectionUtils.FindStaticFields();
        }
    }

    [HarmonyPatch]
    public class Plugin : IModEntrypoint
    {
        private static IConsoleWriter logger;

        public const string Guid = PluginInfo.PLUGIN_GUID;
        public const string Version = PluginInfo.PLUGIN_VERSION;

        public void Entry(IMod mod, IConsoleWriter consoleWriter)
        {
            logger = consoleWriter;
            Log($"Plugin {Guid} is loaded!");

            ReplayConfig config = mod.Configs.Get<ReplayConfig>();
            EventIO.Config = config;
            if (config.GetNetMode() == NetMode.None) return;

            Harmony harmony = new Harmony(Guid);
            harmony.PatchAll();
            //DeterminismPatcher.PatchDeterminism(harmony);

        }

        public static void Log(string message)
        {
            logger.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            logger.LogWarning(message);
        }

        public static void LogError(string message)
        {
            logger.LogError(message);
        }

        public static void LogStackTrace()
        {
            logger.LogInfo(new StackTrace().ToString());
        }
    }
}
