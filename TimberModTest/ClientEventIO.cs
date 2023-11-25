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

        public override bool PlayRecordedEvents => false;

        public ClientEventIO(string address, int port, MapReceived mapReceivedCallback)
        {
            netBase = new TimberClient(address, port);
            netBase.OnMapReceived += mapReceivedCallback;
            netBase.OnLog += Plugin.Log;
            netBase.Start();
        }
    }
}
