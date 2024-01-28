using TimberNet;
using static TimberNet.TimberNetBase;

namespace TimberModTest
{
    public class ClientEventIO : NetIOBase<TimberClient>
    {
        // If the client receives an event to replay, no matter where it
        // originated, it shouldn't send it *back* to the server, since the
        // server is what sent the event.
        public override bool RecordReplayedEvents => false;

        // Clients don't need to send heartbeats
        public override bool ShouldSendHeartbeat => false;

        // The client doesn't get to do anything from the user directly.
        // The client should send user-initiated events to the server.
        // It has to wait until an event is received from the server.
        public override UserEventBehavior UserEventBehavior => UserEventBehavior.Send;

        public ClientEventIO(string address, int port, MapReceived mapReceivedCallback)
        {
            netBase = new TimberClient(address, port);
            netBase.OnMapReceived += mapReceivedCallback;
            netBase.OnLog += Plugin.Log;
            netBase.Start();
        }
    }
}
