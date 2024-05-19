using Newtonsoft.Json.Linq;
using System;
using TimberNet;
using System.Threading.Tasks;
using BeaverBuddies.Events;

namespace BeaverBuddies
{
    // TODO: With TimberBorn's current architecture, we cannot feasibly
    // support joining after the game has started. This is because a number
    // of variables are randomly initialized during load (e.g. tree lifespans)
    // rather than serialized, since they aren't that important. As a result, the
    // save won't create the same gamestate as a currently loaded game. The only
    // way to ensure that the gamestate is the same is to have all clients join
    // at load time.
    // Barring a complete overhaul of how loading works, I should probably disable
    // joining after the play button is first pressed.
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

        public void Start(int port, Func<Task<byte[]>> mapProvider, Func<int> ticksSinceLoadProvider)
        {
            netBase = new TimberServer(port, mapProvider, CreateInitEvent(ticksSinceLoadProvider));
            //netBase = new TimberServer(port, mapProvider, null);
            netBase.OnLog += Plugin.Log;
            netBase.OnMapReceived += NetBase_OnClientConnected;
            netBase.Start();
        }

        private Func<JObject> CreateInitEvent(Func<int> ticksSinceLoadProvider)
        {
            // It should be ok to send an init event even if the client is joining before
            // the server, since a) it won't do much on the Host (just set the random seed)
            // and b) the client will overwrite these values later whent he Host finished
            // loading the map.
            return () =>
            {
                var message = InitializeClientEvent.Create(ticksSinceLoadProvider());
                message.ticksSinceLoad = 0;
                Plugin.Log($"Sending start state: {JsonSettings.Serialize(message)}");
                return JObject.Parse(JsonSettings.Serialize(message));
            };
        }

        public void UpdateProviders(Func<Task<byte[]>> mapProvider, Func<int> ticksSinceLoadProvider)
        {
            netBase.UpdateProviders(mapProvider, CreateInitEvent(ticksSinceLoadProvider));
        }

        private void NetBase_OnClientConnected(byte[] mapBytes)
        {
            
        }
    }
}
