using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BeaverBuddies.Steam
{
    class SteamOverlayConnectionService : IUpdatableSingleton
    {
        private SteamManager _steamManager;
        private ClientConnectionService _clientConnectionService;
        private DialogBoxShower _dialogBoxShower;

        private static List<IDisposable> callbacks = new List<IDisposable>();

        public SteamOverlayConnectionService(
            SteamManager steamManager,
            ClientConnectionService clientConnectionService,
            DialogBoxShower dialogBoxShower
            ) 
        {
            _steamManager = steamManager;
            _clientConnectionService = clientConnectionService;
            _dialogBoxShower = dialogBoxShower;
            //GameObject obj = new GameObject("SteamManagerHost");
            //obj.AddComponent<SteamManager>();
        }

        bool done = false;
        public void UpdateSingleton()
        {
            //Read();
            if (!done)
            {
                if (_steamManager.Initialized)
                {
                    foreach (var callback in callbacks)
                    {
                        callback.Dispose();
                    }

                    string name = SteamFriends.GetPersonaName();
                    Plugin.Log(name);
                    done = true;

                    // TODO: Find a way to remove these callbacks and
                    // re-handle them when the MainMenu is reloaded...

                    //Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                    //Callback<LobbyInvite_t>.Create(OnLobbyInvite);
                    //Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
                    callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested));
                    callbacks.Add(Callback<LobbyEnter_t>.Create(OnLobbyEntered));
                    callbacks.Add(Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest));

                    //SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);

                }
                else
                {
                    Plugin.Log("Waiting on Steamworks to initialize...");
                }
            }
            //ReceiveMessages();
        }

        private void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            string name = SteamFriends.GetFriendPersonaName(callback.m_steamIDFriend);
            Debug.Log("User " + name + " has requested to join the lobby; joining...");
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            Debug.Log("Lobby chat update: " + callback.m_ulSteamIDLobby);
            if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                CSteamID userJoined = new CSteamID(callback.m_ulSteamIDUserChanged);
                string name = SteamFriends.GetFriendPersonaName(userJoined);
                Debug.Log("User " + name + " has joined the lobby.");


                //SteamNetworking.CreateP2PConnectionSocket(memberId, 0, )
                string message = "Hello, beaver buddy!";
                byte[] data = Encoding.UTF8.GetBytes(message);
                SteamNetworking.SendP2PPacket(userJoined, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
            }
        }

        private void OnLobbyInvite(LobbyInvite_t param)
        {
            string invitingUser = SteamFriends.GetFriendPersonaName(new CSteamID(param.m_ulSteamIDUser));
            Plugin.Log($"Invited to lobby {param.m_ulSteamIDLobby} by {invitingUser}");
        }

        private void ReceiveMessages()
        {
            uint messageSize;
            // Check if there are packets available
            while (SteamNetworking.IsP2PPacketAvailable(out messageSize))
            {
                byte[] buffer = new byte[messageSize];
                uint bytesRead;
                CSteamID remoteSteamID;

                // Read the incoming packet
                if (SteamNetworking.ReadP2PPacket(buffer, messageSize, out bytesRead, out remoteSteamID))
                {
                    // Process the received data
                    UnityEngine.Debug.Log("Received message from: " + remoteSteamID);
                    UnityEngine.Debug.Log("Data: " + System.Text.Encoding.UTF8.GetString(buffer));
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
            //int memberCount = SteamMatchmaking.GetNumLobbyMembers(new CSteamID(callback.m_ulSteamIDLobby));
            //for (int i = 0; i < memberCount; i++)
            //{
            //    CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(new CSteamID(callback.m_ulSteamIDLobby), i);
            //    string name = SteamFriends.GetFriendPersonaName(memberId);
            //    UnityEngine.Debug.Log("Lobby member: " + name);
            //}

            var owner = SteamMatchmaking.GetLobbyOwner(new CSteamID(callback.m_ulSteamIDLobby));
            if (owner != SteamUser.GetSteamID())
            {
                Plugin.Log("Joining another's lobby...");
                bool success = _clientConnectionService.TryToConnect(owner);

                // TOOD: loc
                _dialogBoxShower.Create()
                    .SetLocalizedMessage(success ? 
                    "Joined! Receiving map..." : 
                    "Failed to join lobby.")
                    .Show();
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            // Handle the callback
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                // Lobby created successfully
                CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
                bool joinable = SteamMatchmaking.SetLobbyJoinable(lobbyId, true);
                Debug.Log($"Lobby created with ID: {lobbyId} is joinable={joinable}");

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
