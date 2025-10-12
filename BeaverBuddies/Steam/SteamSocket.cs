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
        // Steam can only buffer 1MB at a time, so we need to
        // leave time for that to clear out. Hopefully this is enough.
        // 1 MB per 8 seconds
        const int BYTES_PER_SECOND = 1024 * 1024 / 8;
        // Note this doesn't affect the theoretical issue of multiple
        // events being sent in a single packet - this could split them up
        // but that's fine. So we just choose a moderate size.
        const int MAX_CHUNK_SIZE = 1024 * 8; // 8KB

        public int MaxBytesPerSecond => BYTES_PER_SECOND;
        public int MaxChunkSize => MAX_CHUNK_SIZE;


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
            // This is the client joining, and this only gets called when
            // we've already joined the lobby. It automatically closes
            // the prior client (I think).
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
                // This will fail we ever receive multiple messages in a single packet.
                // I don't think that can happen right now unless Steam merges packets, which
                // seems not to happen... but we should log a more useful
                // warning. And right now the "readOffset" should always be 0.
                Plugin.LogWarning($"SteamSocket read {bytesToCopy} bytes, but {result.Length - bytesToCopy} bytes were left over. This is probably a bug!");
                readOffset = bytesToCopy;
            }
            //Plugin.Log($"SteamSocket receiving {bytesToCopy} bytes");

            return bytesToCopy;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (count > MaxChunkSize)
            {
                throw new IOException($"Attempted to write {buffer.Length} bytes, which exceeds the max chunk size of {MaxChunkSize} bytes.");
            }
            if (offset > 0)
            {
                // Make a copy to avoid modifying the caller's buffer
                byte[] newBuffer = new byte[count];
                Array.Copy(buffer, offset, newBuffer, 0, count);
                buffer = newBuffer;
            }
            Plugin.Log($"SteamSocket sending {count} bytes");
            SteamNetworking.SendP2PPacket(friendID, buffer, (uint)count, EP2PSend.k_EP2PSendReliable);
        }

        public void ReceiveData(byte[] data)
        {
            readBuffer.Enqueue(data);
        }
    }
}
