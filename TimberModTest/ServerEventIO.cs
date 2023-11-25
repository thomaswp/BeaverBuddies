using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using static TimberNet.TimberNetBase;
using TimberNet;
using Timberborn.Beavers;
using System.Threading.Tasks;

namespace TimberModTest
{
    public class ServerEventIO : NetIOBase<TimberServer>
    {
        public override bool PlayRecordedEvents => false;

        public ServerEventIO(int port, Func<Task<byte[]>> mapProvider)
        {
            netBase = new TimberServer(port, mapProvider, () =>
            {
                int seed = UnityEngine.Random.RandomRangeInt(int.MinValue, int.MaxValue);
                UnityEngine.Random.InitState(seed);
                return JObject.Parse(JsonSettings.Serialize(new RandomStateSetEvent()
                {
                    seed = seed
                }
                ));
            });
            netBase.OnLog += Plugin.Log;
            netBase.OnMapReceived += NetBase_OnClientConnected;
            netBase.Start();
        }

        private void NetBase_OnClientConnected(byte[] mapBytes)
        {
            
        }
    }
}
