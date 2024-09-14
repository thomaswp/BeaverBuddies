using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeaverBuddies.Steam
{
    class SteamInitializer : IUpdatableSingleton
    {
        public SteamInitializer() 
        {
            GameObject obj = new GameObject("SteamManagerHost");
            obj.AddComponent<SteamManager>();
        }

        bool done = false;
        public void UpdateSingleton()
        {
            if (!done)
            {
                if (SteamManager.Initialized)
                {
                    string name = SteamFriends.GetPersonaName();
                    Plugin.Log(name);
                    done = true;
                }
                else
                {
                    Plugin.Log("Waiting on Steamworks to initialize...");
                }
            }
        }
    }
}
