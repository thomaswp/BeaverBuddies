using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Timberborn.BuildingsUI;
using Timberborn.Workshops;
using TimberNet;
using UnityEngine.PlayerLoop;

namespace BeaverBuddies.Steam
{
    public class SteamSocket : ISocketStream, ISteamPacketReceiver
    {

        public bool Connected { get; private set; }

        public string Name { get; private set; }

        public readonly CSteamID friendID;
        //public readonly CSteamID lobbyID;

        private readonly ConcurrentQueueWithWait<byte[]> readBuffer = new ConcurrentQueueWithWait<byte[]>();
        private int readOffset = 0;

        private SteamPacketListener packetListener;

        public SteamSocket(CSteamID friendID, bool autoconnect = false)
        {
            this.friendID = friendID;
            Name = SteamFriends.GetFriendPersonaName(friendID);
            Connected = autoconnect;
        }

        public void RegisterSteamPacketListener(SteamPacketListener listener)
        {
            packetListener = listener;
            listener.RegisterSocket(this);
        }

        public Task ConnectAsync()
        {
            // TODO: Check if we're already successfully connected
            Connected = true;
            Plugin.Log("SteamSocket requested to connect!");
            return Task.CompletedTask;
        }

        public void Close()
        {
            Connected = false;
            packetListener?.UnregisterSocket(this);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            // Block until we've read something
            byte[] result;
            while (!readBuffer.WaitAndTryDequeue(out result)) { }
            int bytesToCopy = Math.Min(count, result.Length - readOffset);
            Array.Copy(result, readOffset, buffer, offset, bytesToCopy);
            if (result.Length > bytesToCopy)
            {
                // TODO: This will fail we ever receive a packet that
                // spans multiple messages. I don't think that can happen right
                // now unless things get jumbled, but we should definitely save the rest
                // of result (right now it gets discarded) and read from that first if
                // it exists.
                Plugin.LogWarning($"SteamSocket read {bytesToCopy} bytes, but {result.Length - bytesToCopy} bytes were left over. This is probably a bug!");
                readOffset = bytesToCopy;
            }
            Plugin.Log($"SteamSocket receiving {bytesToCopy} bytes");

            return bytesToCopy;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (offset > 0)
            {
                byte[] newBuffer = new byte[count];
                Array.Copy(buffer, offset, newBuffer, 0, count);
                buffer = newBuffer;
            }
            // TODO: Remove for privacy!
            Plugin.Log($"SteamSocket sending {count} bytes to {Name}");
            SteamNetworking.SendP2PPacket(friendID, buffer, (uint)count, EP2PSend.k_EP2PSendReliable);
        }

        public void ReceiveData(byte[] data)
        {
            readBuffer.Enqueue(data);
        }
    }
}
