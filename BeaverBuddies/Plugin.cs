using BeaverBuddies.Connect;
using Bindito.Core;
using HarmonyLib;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using TimberApi.ConfiguratorSystem;
using TimberApi.ConsoleSystem;
using TimberApi.ModSystem;
using TimberApi.SceneSystem;

namespace BeaverBuddies
{
    [Configurator(SceneEntrypoint.InGame)]  // This attribute registers the configurator and tells where it should be loaded
    public class ReplayConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            if (EventIO.Config.GetNetMode() == NetMode.None) return;

            // Reset everything before loading singletons
            SingletonManager.Reset();

            Plugin.Log($"Registering In Game Services");

            // Add client connection Singletons, since we can now
            // connect from the in-game Options menu (even if we're not
            // playing co-op right now).
            containerDefinition.Bind<ClientConnectionService>().AsSingleton();
            containerDefinition.Bind<ClientConnectionUI>().AsSingleton();

            // EventIO gets set before load, so if it's null, this is a regular
            // game, so don't initialize these services.
            if (EventIO.IsNull) return;

            Plugin.Log("Registering Co-op services");
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
            containerDefinition.Bind<ClientConnectionUI>().AsSingleton();
            containerDefinition.Bind<FirstTimerService>().AsSingleton();

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
        public static IMod Mod { get; private set; }

        public const string Guid = PluginInfo.PLUGIN_GUID;
        public const string Version = PluginInfo.PLUGIN_VERSION;

        public void Entry(IMod mod, IConsoleWriter consoleWriter)
        {
            Mod = mod;
            logger = consoleWriter;

            ReplayConfig config = mod.Configs.Get<ReplayConfig>();
            EventIO.Config = config;

            Log($"Plugin {Guid} is loaded!");

            if (config.GetNetMode() == NetMode.None) return;

            Harmony harmony = new Harmony(Guid);
            harmony.PatchAll();
            //DeterminismPatcher.PatchDeterminism(harmony);
        }

        public static string GetWithDate(string message)
        {
            return $"[{System.DateTime.Now.ToString("HH-mm-ss.ff")}] {message}";
        }

        public static void Log(string message)
        {
            if (!EventIO.Config.Verbose) return;
            logger.LogInfo(GetWithDate(message));
        }

        public static void LogWarning(string message)
        {
            logger.LogWarning(GetWithDate(message));
        }

        public static void LogError(string message)
        {
            logger.LogError(GetWithDate(message));
        }

        public static void LogStackTrace()
        {
            logger.LogInfo(new StackTrace().ToString());
        }
    }
}
