using System;
using System.Collections.Generic;
using System.Text;
using TimberApi.ConfigSystem;

namespace TimberModTest
{
    public enum NetMode { Server, Client, None }

    public class ReplayConfig : IConfig
    {
        public const string MODE_SERVER = "Server";
        public const string MODE_CLIENT = "Client";
        public const string MODE_NONE = "None";

        public string ConfigFileName => "ReplayConfig";

        public string Mode { get; set; } = MODE_NONE;
        public string ClientConnectionAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 25565;

        public NetMode GetNetMode()
        {
            switch (Mode)
            {
                case MODE_SERVER:
                    return NetMode.Server;
                case MODE_CLIENT:
                    return NetMode.Client;
                case MODE_NONE:
                    return NetMode.None;
                default:
                    Plugin.LogWarning("Unknown netowrking mode: " + Mode);
                    return NetMode.None;
            }
        }

    }
}
