﻿using System;
using TimberNet;
using static TimberNet.TimberNetBase;

namespace BeaverBuddies.IO
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

        private MapReceived mapReceivedCallback;
        private bool FailedToConnect = false;

        private ClientEventIO(ISocketStream socket, MapReceived mapReceivedCallback,
            Action<string> onError)
        {
            this.mapReceivedCallback = mapReceivedCallback;

            TryRegisterSteamPacketReceiver(socket);

            NetBase = new TimberClient(socket);
            NetBase.OnMapReceived += mapReceivedCallback;
            NetBase.OnLog += Plugin.Log;
            NetBase.OnError += (error) =>
            {
                Plugin.LogError(error);
                CleanUp();
                FailedToConnect = true;
                onError(error);
            };
            try
            {
                NetBase.Start();
            }
            catch (Exception ex)
            {
                Plugin.LogError(ex.ToString());
                CleanUp();
                FailedToConnect = true;
            }
        }

        private void CleanUp()
        {
            if (NetBase == null) return;
            NetBase.OnMapReceived -= mapReceivedCallback;
            NetBase.OnLog -= Plugin.Log;
            NetBase = null;
        }

        public static ClientEventIO Create(ISocketStream socket, MapReceived mapReceivedCallback, Action<string> onError)
        {
            ClientEventIO eventIO = new ClientEventIO(socket, mapReceivedCallback, onError);
            if (eventIO.FailedToConnect) return null;
            return eventIO;
        }
    }
}
