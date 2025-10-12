﻿#define IS_STEAM

#if IS_STEAM
using Timberborn.SteamStoreSystem;
#endif

using BeaverBuddies.Connect;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace BeaverBuddies.Steam
{
    class SteamOverlayConnectionService : IUpdatableSingleton
    {
        public static bool IsSteamEnabled { get; private set; } = false;

#if IS_STEAM
        private SteamManager _steamManager;
        private ClientConnectionService _clientConnectionService;

        private static List<IDisposable> callbacks = new List<IDisposable>();

        public SteamOverlayConnectionService(
            SteamManager steamManager,
            ClientConnectionService clientConnectionService
            )
        {
            _steamManager = steamManager;
            _clientConnectionService = clientConnectionService;
        }

        bool done = false;
        public void UpdateSingleton()
        {
            //Read();
            if (!done)
            {
                if (_steamManager.Initialized)
                {
                    IsSteamEnabled = true;

                    foreach (var callback in callbacks)
                    {
                        callback.Dispose();
                    }

                    done = true;

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

        private void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            CSteamID clientId = callback.m_steamIDRemote;
            SteamNetworking.AcceptP2PSessionWithUser(clientId);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            var owner = SteamMatchmaking.GetLobbyOwner(new CSteamID(callback.m_ulSteamIDLobby));
            if (owner != SteamUser.GetSteamID())
            {
                Plugin.Log("Joining another's lobby...");
                bool success = _clientConnectionService.TryToConnect(owner);
                _clientConnectionService.ShowConnectionMessage(success);
            }
        }
#else
        // Need to implement the interface
        public void UpdateSingleton() { }
#endif
    }
}
