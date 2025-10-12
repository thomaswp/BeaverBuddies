using BeaverBuddies.IO;
using BeaverBuddies.Steam;
using BeaverBuddies.Util;
using Steamworks;
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
    public class ClientConnectionService : IUpdatableSingleton
    {
        private GameSceneLoader _gameSceneLoader;
        private GameSaveRepository _gameSaveRepository;
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;
        private ClientEventIO client;
        private Settings _settings;

        public ClientConnectionService(
            GameSceneLoader gameSceneLoader,
            GameSaveRepository gameSaveRepository,
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            Settings settings
        )
        {
            _gameSceneLoader = gameSceneLoader;
            _gameSaveRepository = gameSaveRepository;
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
            _settings = settings;
        }

        public bool TryToConnect(CSteamID friendID)
        {
            return TryToConnect(new SteamSocket(friendID));
        }

        public bool TryToConnect(string address)
        {
            int port = _settings.DefaultPort.Value;
            Plugin.Log("Try to resolve address: " + address);
            try
            {
                // Parse address and port
                var (hostAddress, parsedPort) = ParseAddressAndPort(address);

                // Set port if provided
                if (parsedPort.HasValue)
                {
                    port = parsedPort.Value;
                    Plugin.Log("Using parsed port: " + port);
                }

                // Resolve the address if it's a hostname
                hostAddress = ResolveHostnameIfNecessary(hostAddress);
            }
            catch (Exception ex)
            {
                // TODO: I think it'd be better to have specific messages for parsing errors,
                // rather than using a connection failed message with more details.
                // It also shows the error twice.
                ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessageWithError", ex.Message);
                return false;
            }

            return TryToConnect(new TCPClientWrapper(address, port));
        }

        private bool TryToConnect(ISocketStream socket)
        {
            Plugin.Log("Connecting client");
            client = ClientEventIO.Create(socket, LoadMap, (error) =>
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

        public void ConnectOrShowFailureMessage()
        {
            ConnectOrShowFailureMessage(_settings.ClientConnectionAddress.Value);
        }

        public void ConnectOrShowFailureMessage(string address)
        {
            if (TryToConnect(address)) return;

            ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessage");
        }

        public void ShowConnectionMessage(bool success)
        {
            if (success)
            {
                _dialogBoxShower.Create()
                    .SetLocalizedMessage("BeaverBuddies.JoinCoopGame.Success")
                    .Show();
            }
            else
            {
                ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessage");
            }
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
                    // TODO: Loc!
                    throw new FormatException("Invalid address format. Could not parse port.");
                }
            }
            return (address, null);
        }

        private string ResolveHostnameIfNecessary(string address)
        {
            // If it's not an IP address, resolve the hostname
            if (IPAddress.TryParse(address, out _))
            {
                return address;
            }

            // Otherwise, try to resolve it
            IPHostEntry hostEntry = Dns.GetHostEntry(address);
            if (hostEntry.AddressList.Length > 0)
            {
                string resolvedAddress = hostEntry.AddressList[0].ToString();
                Plugin.Log(address + " resolved to " + resolvedAddress);
                return resolvedAddress;
            }
            else
            {
                // TODO: Loc!
                throw new Exception("Hostname could not be resolved to an IP address.");
            }
        }
    }
}
