using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BeaverBuddies.Steam
{
    class SteamInitializer : IUpdatableSingleton
    {
        private SteamManager _steamManager;

        public SteamInitializer(SteamManager steamManager) 
        {
            _steamManager = steamManager;
            //GameObject obj = new GameObject("SteamManagerHost");
            //obj.AddComponent<SteamManager>();
        }

        bool done = false;
        public void UpdateSingleton()
        {
            Read();
            if (!done)
            {
                if (_steamManager.Initialized)
                {
                    string name = SteamFriends.GetPersonaName();
                    Plugin.Log(name);
                    done = true;

                    SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
                    Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                    Callback<LobbyEnter_t>.Create(OnLobbyEntered);

                    Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
                }
                else
                {
                    Plugin.Log("Waiting on Steamworks to initialize...");
                }
            }
        }

        void Read()
        {
            uint size;

            // repeat while there's a P2P message available
            // will write its size to size variable
            while (SteamNetworking.IsP2PPacketAvailable(out size))
            {
                // allocate buffer and needed variables
                var buffer = new byte[size];
                uint bytesRead;
                CSteamID remoteId;

                // read the message into the buffer
                if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId))
                {
                    // convert to string
                    char[] chars = new char[bytesRead / sizeof(char)];
                    Buffer.BlockCopy(buffer, 0, chars, 0, chars.Length);

                    string message = new string(chars, 0, chars.Length);
                    Debug.Log($"Received a message: {message} from {remoteId}");
                }
            }
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            CSteamID clientId = callback.m_steamIDRemote;
            SteamNetworking.AcceptP2PSessionWithUser(clientId);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(new CSteamID(callback.m_ulSteamIDLobby));
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(new CSteamID(callback.m_ulSteamIDLobby), i);
                string name = SteamFriends.GetFriendPersonaName(memberId);
                UnityEngine.Debug.Log("Lobby member: " + name);
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            // Handle the callback
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                // Lobby created successfully
                CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
                UnityEngine.Debug.Log("Lobby created with ID: " + lobbyId);

                //SteamFriends.ActivateGameOverlayInviteDialog(lobbyId);
            }
            else
            {
                // Handle error
                UnityEngine.Debug.LogError("Failed to create lobby: " + callback.m_eResult);
            }
        }
    }
}
