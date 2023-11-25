using System;
using System.Collections.Generic;
using TimberNet;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using static TimberNet.TimberNetBase;

namespace TimberModTest
{
    public class ClientEventIO : NetIOBase<TimberClient>
    {

        // The client doesn't get to do anything from the user directy.
        // It has to wait until an event is received from the server.
        public override bool PlayRecordedEvents => false;
        // If the client receives an event to replay, no matter where it
        // originated, it shouldn't send it *back* to the server, since the
        // server is what send the event.
        public override bool RecordReplayedEvents => false;

        public ClientEventIO(string address, int port, MapReceived mapReceivedCallback)
        {
            netBase = new TimberClient(address, port);
            netBase.OnMapReceived += mapReceivedCallback;
            netBase.OnLog += Plugin.Log;
            netBase.Start();
        }
    }
}
