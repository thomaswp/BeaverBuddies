using Newtonsoft.Json;
using System;
using System.IO;
using Timberborn.Modding;

namespace BeaverBuddies
{
    [Obsolete("Use Settings instead")]
    public class ReplayConfig
    {
        public string ClientConnectionAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 25565;
        public bool Verbose = true;
        public bool FirstTimer = true;
        public bool ReportingConsent = false;
        public bool AlwaysDebug = false;
    }
}
