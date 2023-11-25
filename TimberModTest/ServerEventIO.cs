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
                RandomStateSetEvent message = new RandomStateSetEvent()
                {
                    seed = seed
                };
                // TODO: Not certain if this is the right time, or if it should be enqueued
                message.Replay(null);
                return JObject.Parse(JsonSettings.Serialize(message));
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
