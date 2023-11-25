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
        // At some point we may need to change this to false if
        // playing recoded events instantly creates different effects
        // than waiting until the end of a tick, but for now we assume
        // it doesn't.
        public override bool PlayUserEvents => true;
        // Anything that happens on the server should be recorded and
        // sent to the clients.
        public override bool RecordReplayedEvents => true;

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
