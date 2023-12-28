using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using static TimberNet.TimberNetBase;
using TimberNet;
using Timberborn.Beavers;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using TimberModTest.Events;

namespace TimberModTest
{
    public class ServerEventIO : NetIOBase<TimberServer>
    {
        // Anything that happens on the server should be recorded and
        // sent to the clients.
        public override bool RecordReplayedEvents => true;

        // Servers need to send heartbeats so clients know to progress.
        public override bool ShouldSendHeartbeat => true;

        // The server should wait until the next update to play a
        // user-initiated event, to make sure that the events
        // happen in the same order for the server and clients.
        // TBH this may not be necessary.
        public override UserEventBehavior UserEventBehavior => UserEventBehavior.QueuePlay;

        public ServerEventIO(int port, Func<Task<byte[]>> mapProvider, Func<int> ticksSinceLoadProvider)
        {
            netBase = new TimberServer(port, mapProvider, () =>
            {
                var message = RandomStateSetEvent.CreateAndExecute(ticksSinceLoadProvider());
                message.ticksSinceLoad = 0;
                Plugin.Log($"Sending start state: {JsonSettings.Serialize(message)}");
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
