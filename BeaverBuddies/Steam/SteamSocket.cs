using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimberNet;
using UnityEngine.PlayerLoop;

namespace BeaverBuddies.Steam
{
    public class SteamSocket : ISocketStream
    {
        // A little over a frame, so the update should be called again
        public const int AWAIT_THREAD_SLEEP_INTERVAL_MS = 25;

        public bool Connected { get; private set; }

        public readonly CSteamID friendID;
        //public readonly CSteamID lobbyID;

        private readonly ConcurrentQueue<byte[]> readBuffer = new ConcurrentQueue<byte[]>();
        private int readOffset = 0;

        public SteamSocket(CSteamID friendID)
        {
            this.friendID = friendID;
        }

        public Task ConnectAsync()
        {
            // TODO: Check if we're already successfully connected
            Connected = true;
            return Task.CompletedTask;
        }

        public void Close()
        {
            Connected = false;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            // Block until we've read something
            byte[] result;
            while (!readBuffer.TryDequeue(out result))
            {
                Thread.Sleep(AWAIT_THREAD_SLEEP_INTERVAL_MS);
            }
            int bytesToCopy = Math.Min(count, result.Length - readOffset);
            Array.Copy(result, readOffset, buffer, offset, bytesToCopy);
            if (result.Length > bytesToCopy)
            {
                readOffset = bytesToCopy;
            }

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
            SteamNetworking.SendP2PPacket(friendID, buffer, (uint)count, EP2PSend.k_EP2PSendReliable);
        }

        public void ReceiveData(byte[] data)
        {
            readBuffer.Enqueue(data);
        }
    }
}
