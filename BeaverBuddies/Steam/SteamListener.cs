using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TimberNet;

namespace BeaverBuddies.Steam
{
    public class SteamListener : ISocketListener, ISteamPacketReceiver
    {
        // TODO: When tested, use ConcurrentQueueWithWaitInstead
        public const int AWAIT_THREAD_SLEEP_INTERVAL_MS = 250;

        public CSteamID LobbyID { get; private set; }

        private List<IDisposable> callbacks = new List<IDisposable>();
        private Dictionary<CSteamID, SteamSocket> sockets = new Dictionary<CSteamID, SteamSocket>();
        private ConcurrentQueue<SteamSocket> joiningUsers = new ConcurrentQueue<SteamSocket>();
        private SteamPacketListener steamPacketListener;

        public void RegisterSteamPacketListener(SteamPacketListener steamPacketListener)
        {
            this.steamPacketListener = steamPacketListener;
        }

        public void Start()
        {
            if (steamPacketListener == null)
            {
                throw new InvalidOperationException("SteamPacketListener must be registered before starting the SteamListener.");
            }
            Plugin.Log("SteamListener started...");
            callbacks.Add(Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate));
            callbacks.Add(Callback<LobbyCreated_t>.Create(OnLobbyCreated));
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            // Handle the callback
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                // Lobby created successfully
                LobbyID = new CSteamID(callback.m_ulSteamIDLobby);
                // TODO: Make configurable
                bool joinable = SteamMatchmaking.SetLobbyJoinable(LobbyID, true);
                Plugin.Log($"Lobby created with ID: {LobbyID} is joinable={joinable}");

                // TODO: Maybe?
                //SteamFriends.ActivateGameOverlayInviteDialog(LobbyID);
            }
            else
            {
                // Handle error
                Plugin.LogError("Failed to create lobby: " + callback.m_eResult);
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            Plugin.Log("Lobby chat update: " + callback.m_ulSteamIDLobby);
            if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                CSteamID userJoined = new CSteamID(callback.m_ulSteamIDUserChanged);
                string name = SteamFriends.GetFriendPersonaName(userJoined);
                // TODO: Remove for privacy
                Plugin.Log("User " + name + " has joined the lobby.");

                var socket = new SteamSocket(userJoined, true);
                steamPacketListener.RegisterSocket(socket);
                sockets.Add(userJoined, socket);
                joiningUsers.Enqueue(socket);

                //SteamNetworking.CreateP2PConnectionSocket(memberId, 0, )
                //string message = "Hello, beaver buddy!";
                //byte[] data = Encoding.UTF8.GetBytes(message);
                //SteamNetworking.SendP2PPacket(userJoined, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
            }
        }

        public ISocketStream AcceptClient()
        {
            Plugin.Log("Waiting to accept a client...");
            SteamSocket socket;
            while (!joiningUsers.TryDequeue(out socket))
            {
                Thread.Sleep(AWAIT_THREAD_SLEEP_INTERVAL_MS);
            }
            Plugin.Log("New client accepted!");
            return socket;
        }

        public void Stop()
        {
            Plugin.Log("Stopping listener...");
            SteamMatchmaking.LeaveLobby(LobbyID);
            foreach (IDisposable callback in callbacks)
            {
                callback.Dispose();
            }
        }

        // TODO: Need this to be called
        public void Update()
        {
            
        }
    }
}
