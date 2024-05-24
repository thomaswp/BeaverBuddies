using BeaverBuddies.Connect;
using BeaverBuddies.DesyncDetecter;
using Bindito.Core;
using HarmonyLib;
using Open.Nat;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TimberApi.ConfiguratorSystem;
using TimberApi.ConsoleSystem;
using TimberApi.ModSystem;
using TimberApi.SceneSystem;
using Timberborn.TickSystem;
using UnityEngine.XR;

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
            //containerDefinition.Bind<ServerConnectionService>().AsSingleton();
            containerDefinition.Bind<ReplayService>().AsSingleton();
            containerDefinition.Bind<RecordToFileService>().AsSingleton();
            containerDefinition.Bind<TickProgressService>().AsSingleton();
            containerDefinition.Bind<TickingService>().AsSingleton();
            containerDefinition.Bind<DeterminismService>().AsSingleton();
            containerDefinition.Bind<RehostingService>().AsSingleton();
            containerDefinition.Bind<DesyncDetecterService>().AsSingleton();

        }
    }

    public class Listener : TraceListener
    {
        public override void Write(string message)
        {
            Console.Write(message);
        }

        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
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
            //ReflectionUtils.PrintChildClasses(typeof(IParallelTickableSingleton));
            //ReflectionUtils.FindStaticFields();
            //ReflectionUtils.FindHashSetFields();

            var discoverer = new NatDiscoverer();

            NatDiscoverer.TraceSource.Switch.Level = SourceLevels.Verbose;
            NatDiscoverer.TraceSource.Listeners.Add(new Listener());

            Task.Run(async () =>
            {
                // using SSDP protocol, it discovers NAT device.
                var device = await discoverer.DiscoverDeviceAsync();

                // display the NAT's IP address
                Console.WriteLine("The external IP Address is: {0} ", await device.GetExternalIPAsync());
                Console.WriteLine(await device.GetSpecificMappingAsync(Protocol.Tcp, 25565));

                // create a new mapping in the router [external_ip:1702 -> host_machine:1602]
                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1602, 1702, "For testing"));

                Console.WriteLine("Success!");

                // configure a TCP socket listening on port 1602
                var endPoint = new IPEndPoint(IPAddress.Any, 1602);
                var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
                socket.Bind(endPoint);
                socket.Listen(4);
            });
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
            //EnumerableFirstPatcher.CreatePatch(harmony);
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
