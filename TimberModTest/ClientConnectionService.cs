using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Timberborn.Core;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.SingletonSystem;
using TimberNet;
using UnityEngine;

namespace TimberModTest
{
    public class ClientConnectionService : IUpdatableSingleton, IPostLoadableSingleton
    {

        public const string LOCALHOST = "127.0.0.1";

        private GameSceneLoader _gameSceneLoader;
        private GameSaveRepository _gameSaveRepository;
        private ClientEventIO client;

        public ClientConnectionService(GameSceneLoader gameSceneLoader, GameSaveRepository gameSaveRepository)
        {
            _gameSceneLoader = gameSceneLoader;
            _gameSaveRepository = gameSaveRepository;
        }

        public void PostLoad()
        {
            //TODO: make a UI!
            if (EventIO.Config.GetNetMode() != NetMode.Client) return;
            Plugin.Log("Connecting client");
            try
            {
                client = new ClientEventIO(EventIO.Config.ClientConnectionAddress, EventIO.Config.Port, LoadMap);
                EventIO.Set(client);

            } catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }

        private void LoadMap(byte[] mapBytes)
        {
            Plugin.Log("Loading map");
            //string saveName = Guid.NewGuid().ToString();
            string saveName = TimberNetBase.GetHashCode(mapBytes).ToString("X8");
            SaveReference saveRef = new SaveReference("Online Games", saveName);
            Stream stream = _gameSaveRepository.CreateSaveSkippingNameValidation(saveRef);
            stream.Write(mapBytes);
            stream.Close();
            _gameSceneLoader.StartSaveGame(saveRef);
        }

        public void UpdateSingleton()
        {
            if (client == null) return;
            //Plugin.Log("Updating client!");
            client.Update();
        }
    }
}
