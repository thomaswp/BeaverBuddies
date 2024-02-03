using System;
using System.IO;
using Timberborn.Core;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.SingletonSystem;
using TimberNet;

namespace BeaverBuddies.Connect
{
    public class ClientConnectionService : IUpdatableSingleton, IPostLoadableSingleton
    {

        public const string LOCALHOST = "127.0.0.1";

        private GameSceneLoader _gameSceneLoader;
        private GameSaveRepository _gameSaveRepository;
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;
        private ClientEventIO client;

        public ClientConnectionService(
            GameSceneLoader gameSceneLoader,
            GameSaveRepository gameSaveRepository,
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener
        )
        {
            _gameSceneLoader = gameSceneLoader;
            _gameSaveRepository = gameSaveRepository;
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
        }

        public void PostLoad()
        {
            if (EventIO.Config.GetNetMode() == NetMode.AutoconnectClient)
            {
                ConnectOrShowFailureMessage(EventIO.Config.ClientConnectionAddress);
            }
        }

        public bool TryToConnect(string address)
        {
            Plugin.Log("Connecting client");
            client = ClientEventIO.Create(address, EventIO.Config.Port, LoadMap);
            if (client == null) return false;
            EventIO.Set(client);
            return true;
        }

        const string ConnectionFailedMessage =
            "Failed to connect to Host. Would you like to open the troubleshooting guide?";
        const string TroubleshootingUrl = "https://github.com/thomaswp/BeaverBuddies/wiki/Installation-and-Running#troubleshooting";

        public void ConnectOrShowFailureMessage(string address)
        {
            if (TryToConnect(address)) return;

            var action = () =>
            {
                _urlOpener.OpenUrl(TroubleshootingUrl);
            };

            _dialogBoxShower.Create()
                .SetMessage(ConnectionFailedMessage)
                .SetConfirmButton(action)
                .SetDefaultCancelButton()
                .Show();
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
