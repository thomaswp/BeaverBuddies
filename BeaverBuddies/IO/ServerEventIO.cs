using Newtonsoft.Json.Linq;
using System;
using TimberNet;
using System.Threading.Tasks;
using BeaverBuddies.Events;
using BeaverBuddies.Steam;
using System.Net.Sockets;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BeaverBuddies.IO
{
    // With TimberBorn's current architecture, we cannot feasibly
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
        public override UserEventBehavior UserEventBehavior => UserEventBehavior.QueuePlay;

        public ISocketListener SocketListener { get; private set; }

        // We only support a static map; see note above
        public void Start(byte[] mapBytes)
        {
            try
            {
                List<ISocketListener> listeners = [
                    new TCPListenerWrapper(Settings.Port)
                ];
                if (SteamOverlayConnectionService.IsSteamEnabled && Settings.EnableSteam)
                {
                    listeners.Add(new SteamListener());
                }
                SocketListener = new MultiSocketListener(listeners.ToArray());
                if (SocketListener is MultiSocketListener)
                {
                    foreach (ISocketListener child in ((MultiSocketListener)SocketListener).Listeners)
                    {
                        TryRegisterSteamPacketReceiver(child);
                    }
                }
                else
                {
                    TryRegisterSteamPacketReceiver(SocketListener);
                }
                NetBase = new TimberServer(
                    SocketListener,
                    () =>
                    {
                        // TODO: Probably don't need to hold it in memory after the first tick...
                        Task<byte[]> task = new Task<byte[]>(() => mapBytes);
                        task.Start();
                        return task;
                    },
                    CreateInitEvent()
                );
            }
            catch (Exception e)
            {
                Plugin.Log("Failed to start server");
                Plugin.Log(e.ToString());
                return;
            }
            //netBase = new TimberServer(port, mapProvider, null);
            NetBase.OnLog += Plugin.Log;
            NetBase.OnMapReceived += NetBase_OnClientConnected;
            NetBase.Start();
        }

        private Func<JObject> CreateInitEvent()
        {
            // It should be ok to send an init event even if the client is joining before
            // the server, since a) it won't do much on the Host (just set the random seed)
            // and b) the client will overwrite these values later whent he Host finished
            // loading the map.
            return () =>
            {
                var message = InitializeClientEvent.Create();
                message.ticksSinceLoad = 0;
                Plugin.Log($"Sending start state: {JsonSettings.Serialize(message)}");
                return JObject.Parse(JsonSettings.Serialize(message));
            };
        }

        public void StopAcceptingClients()
        {
            Plugin.Log("Game started: no longer accepting clients");
            string message = $"The Host has already started the game, and the game can no longer be joined. " +
                $"Ask the Host to rehost and join before they unpause.";
            NetBase.StopAcceptingClients(message);
            // TODO: remove map from memory
        }

        private void NetBase_OnClientConnected(byte[] mapBytes)
        {

        }
    }
}
