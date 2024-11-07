using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.IO;
using System.Net.Sockets;
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

        public bool TryToConnect(string address)
        {
            // Clean up our current co-op state before connecting,
            // so we don't, for example, end up ticking the client before
            // it's actually loaded.
            SingletonManager.Reset();
            Plugin.Log("Connecting client");
            Plugin.Log("Try to resolve address: " + address);

            try
            {
                // Parse address and port
                var (hostAddress, port) = ParseAddressAndPort(address);

                // Set port if provided
                if (port.HasValue)
                {
                    EventIO.Config.Port = port.Value;
                }

                // Resolve the address if it's a hostname
                hostAddress = ResolveHostnameIfNecessary(hostAddress);

                // Attempt to create the client
                client = ClientEventIO.Create(hostAddress, EventIO.Config.Port, LoadMap, error =>
                {
                    ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessageWithError", error);
                });

                if (client == null)
                {
                    Plugin.Log("Client creation failed.");
                    return false;
                }

                EventIO.Set(client);
                return true;
            }
            catch (Exception ex)
            {
                ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessageWithError", ex.Message);
                return false;
            }
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

        private (string, int?) ParseAddressAndPort(string address)
        {
            // If address contains a port, split and parse it
            if (address.Contains(":"))
            {
                var tokens = address.Split(':');
                if (tokens.Length == 2 && int.TryParse(tokens[1], out int port))
                {
                    return (tokens[0], port);
                }
                else
                {
                    throw new FormatException("Invalid address format. Could not parse port.");
                }
            }
            return (address, null);
        }

        private string ResolveHostnameIfNecessary(string address)
        {
            // If it's not an IP address, resolve the hostname
            if (!IPAddress.TryParse(address, out _))
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(address);
                if (hostEntry.AddressList.Length > 0)
                {
                    return hostEntry.AddressList[0].ToString();
                }
                else
                {
                    throw new Exception("Hostname could not be resolved to an IP address.");
                }
            }
            return address; // If already an IP, return as-is
        }
    }
}
