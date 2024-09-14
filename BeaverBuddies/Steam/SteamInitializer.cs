using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            if (!done)
            {
                if (_steamManager.Initialized)
                {
                    string name = SteamFriends.GetPersonaName();
                    Plugin.Log(name);
                    done = true;

                    var lobby = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
                    Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                }
                else
                {
                    Plugin.Log("Waiting on Steamworks to initialize...");
                }
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

                SteamFriends.ActivateGameOverlayInviteDialog(lobbyId);
            }
            else
            {
                // Handle error
                UnityEngine.Debug.LogError("Failed to create lobby: " + callback.m_eResult);
            }
        }
    }
}
