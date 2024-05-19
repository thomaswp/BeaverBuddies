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
        // Static here makes sense since it should really only
        // every happen once
        private static bool hasAutoloaded = false;

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
            if (!hasAutoloaded && EventIO.Config.GetNetMode() == NetMode.AutoconnectClient)
            {
                hasAutoloaded = true;
                ConnectOrShowFailureMessage(EventIO.Config.ClientConnectionAddress);
            }
        }

        public bool TryToConnect(string address)
        {
            // Clean up our current co-op state before connecting,
            // so we don't, for example, end up ticking the client before
            // it's actually loaded.
            SingletonManager.Reset();
            Plugin.Log("Connecting client");
            client = ClientEventIO.Create(address, EventIO.Config.Port, LoadMap, (message) =>
            {
                message = "Joining failed with error:\n" + message;
                message += "\nWould you like to open the troubleshooting guide?";
                ShowError(message);
            });
            if (client == null) return false;
            EventIO.Set(client);
            return true;
        }

        const string ConnectionFailedMessage =
            "Failed to connect to Host. Would you like to open the troubleshooting guide?";
        const string TroubleshootingUrl = "https://github.com/thomaswp/BeaverBuddies/wiki/Installation-and-Running#troubleshooting";

        public void ConnectOrShowFailureMessage()
        {
            ConnectOrShowFailureMessage(EventIO.Config.ClientConnectionAddress);
        }

        public void ConnectOrShowFailureMessage(string address)
        {
            if (TryToConnect(address)) return;

            ShowError(ConnectionFailedMessage);
        }

        private void ShowError(string message)
        {

            var action = () =>
            {
                _urlOpener.OpenUrl(TroubleshootingUrl);
            };

            _dialogBoxShower.Create()
                .SetMessage(message)
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

            // Set the RNG seed before loading the map
            // The server does the same
            DeterminismService.InitRandomStateWithMapBytes(mapBytes);
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
