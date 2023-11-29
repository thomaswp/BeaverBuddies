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
        // Anything that happens on the server should be recorded and
        // sent to the clients.
        public override bool RecordReplayedEvents => true;

        // The server should wait until the next update to play a
        // user-initiated event, to make sure that the events
        // happen in the same order for the server and clients.
        // TBH this may not be necessary.
        public override UserEventBehavior UserEventBehavior => UserEventBehavior.QueuePlay;

        public ServerEventIO(int port, Func<Task<byte[]>> mapProvider)
        {
            netBase = new TimberServer(port, mapProvider, () =>
            {
                var message = RandomStateSetEvent.CreateAndExecute();
                return JObject.Parse(JsonSettings.Serialize(message));
            });
            //netBase = new TimberServer(port, mapProvider, null);
            netBase.OnLog += Plugin.Log;
            netBase.OnMapReceived += NetBase_OnClientConnected;
            netBase.Start();
        }

        private void NetBase_OnClientConnected(byte[] mapBytes)
        {
            
        }
    }
}
