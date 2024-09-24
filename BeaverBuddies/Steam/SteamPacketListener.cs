using Steamworks;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace BeaverBuddies.Steam
{
    public class SteamPacketListener
    {
        private Dictionary<CSteamID, SteamSocket> sockets = new Dictionary<CSteamID, SteamSocket>();

        public void RegisterSocket(SteamSocket socket)
        {
            sockets[socket.friendID] = socket;
        }

        public void UnregisterSocket(SteamSocket socket)
        {
            sockets.Remove(socket.friendID);
        }

        public void Update()
        {
            Plugin.LogWarning("SPL Update");
            uint messageSize;
            while (SteamNetworking.IsP2PPacketAvailable(out messageSize))
            {
                byte[] buffer = new byte[messageSize];
                uint bytesRead;

                // TODO: Make sure this is for us
                CSteamID remoteSteamID;

                // Read the incoming packet
                if (SteamNetworking.ReadP2PPacket(buffer, messageSize, out bytesRead, out remoteSteamID))
                {
                    // Process the received data
                    Plugin.Log($"Received {messageSize} bytes from: {remoteSteamID}");
                    if (buffer.Length < 1000)
                    {
                        Plugin.Log("Data: " + Encoding.UTF8.GetString(buffer));
                    }

                    if (sockets.ContainsKey(remoteSteamID))
                    {
                        sockets[remoteSteamID].ReceiveData(buffer);
                    }
                    else
                    {
                        Plugin.LogWarning("Received message from unknown user: " + remoteSteamID);
                    }
                }
                else
                {
                    Plugin.LogWarning("Failed to read packet!");
                }
            }
        }
    }
}
