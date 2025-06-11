using BeaverBuddies.IO;
using BeaverBuddies.Steam;
using BeaverBuddies.Util;
using Steamworks;
using System;
using System.IO;
using System.Net;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WebNavigation;
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

        public bool TryToConnect(CSteamID friendID)
        {
            return TryToConnect(new SteamSocket(friendID));
        }

        public bool TryToConnect(string address)
        {
            return TryToConnect(new TCPClientWrapper(address, EventIO.Config.Port));
        }

        private bool TryToConnect(ISocketStream socket)
        {
            Plugin.Log("Connecting client");
            client = ClientEventIO.Create(socket, LoadMap, (error) =>
            {
                ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessageWithError", error);
            });
            if (client == null) return false;
            EventIO.Set(client);
            return true;
        }

        public void ConnectOrShowFailureMessage()
        {
            ConnectOrShowFailureMessage(EventIO.Config.ClientConnectionAddress);
        }

        public void ConnectOrShowFailureMessage(string address)
        {
            if (TryToConnect(address)) return;

            ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessage");
        }

        private void ShowError(string message, string error = null)
        {

            ILoc _loc = _dialogBoxShower._loc;

            var action = () =>
            {
                _urlOpener.OpenUrl(LinkHelper.TroubleshootingUrl);
            };

            message = _loc.T(message, error);
            _dialogBoxShower.Create()
                .SetMessage(message)
                .SetConfirmButton(action)
                .SetDefaultCancelButton()
                .Show();
        }

        private void LoadMap(byte[] mapBytes)
        {
            // Clean up our current co-op state before loading,
            // so we don't, for example, end up ticking the client before
            // it's actually loaded.
            SingletonManager.Reset();

            Plugin.Log("Loading map");
            //string saveName = Guid.NewGuid().ToString();
            string saveName = TimberNetBase.GetHashCode(mapBytes).ToString("X8");
            SaveReference saveRef = new SaveReference("Online Games", saveName);
            Stream stream = _gameSaveRepository.CreateSaveSkippingNameValidation(saveRef);
            stream.Write(mapBytes);
            stream.Close();

            // Set the RNG seed before loading the map
            // The server does the same
            DeterminismService.InitGameStartState(mapBytes);
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
