using TimberApi.ConfigSystem;

namespace TimberModTest
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

    }
}
