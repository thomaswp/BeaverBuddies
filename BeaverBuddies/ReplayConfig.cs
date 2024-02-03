using Newtonsoft.Json;
using System;
using System.IO;
using TimberApi.ConfigSystem;
using TimberApi.ModSystem;

namespace BeaverBuddies
{
    public enum NetMode { Server, Client, Record, Replay, None }

    public class ReplayConfig : IConfig
    {
        public const string MODE_SERVER = "server";
        public const string MODE_CLIENT = "client";
        public const string MODE_RECORD = "record";
        public const string MODE_REPLAY = "replay";
        public const string MODE_NONE = "none";

        public string ConfigFileName => "ReplayConfig";

        public string Mode { get; set; } = MODE_NONE;
        public string ClientConnectionAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 25565;
        public bool Verbose = true;

        public NetMode GetNetMode()
        {
            switch (Mode.ToLower())
            {
                case MODE_SERVER:
                    return NetMode.Server;
                case MODE_CLIENT:
                    return NetMode.Client;
                case MODE_NONE:
                    return NetMode.None;
                case MODE_RECORD:
                    return NetMode.Record;
                case MODE_REPLAY:
                    return NetMode.Replay;
                default:
                    Plugin.LogWarning("Unknown netowrking mode: " + Mode);
                    return NetMode.None;
            }
        }

        public void SaveConfig()
        {
            string configPath = Path.Combine(Plugin.Mod.DirectoryPath, "configs", $"{typeof(ReplayConfig).Name}.json");
            try
            {
                File.WriteAllText(configPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"Failed to save config: {e}");
            }
        }

    }
}
