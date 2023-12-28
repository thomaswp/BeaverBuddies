using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.MapSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using TimberModTest.Events;
using TimberNet;

namespace TimberModTest
{
    class ServerConnectionService : IPostLoadableSingleton, IUpdatableSingleton
    {
        private ServerEventIO io;

        private GameSaver _gameSaver;
        private ReplayService _replayService;

        private TaskCompletionSource<byte[]> mapLoadingSource;

        private bool isLoadingMap = false;

        public ServerConnectionService(GameSaver gameSaver, ReplayService replayService)
        {
            _gameSaver = gameSaver;
            _replayService = replayService;
        }

        public void Start()
        {
            try
            {
                io = new ServerEventIO(EventIO.Config.Port,
                    ProvideGameState(),
                    () => _replayService.TicksSinceLoad);
                EventIO.Set(io);
            } catch (Exception e) {
                Plugin.Log("Failed to start server");
                Plugin.Log(e.ToString());
            }

        }

        private Func<Task<byte[]>> ProvideGameState()
        {
            return () =>
            {
                if (mapLoadingSource == null)
                {
                    isLoadingMap = true;
                    mapLoadingSource = new TaskCompletionSource<byte[]>();
                }
                return mapLoadingSource.Task;
            };
        }

        private void LoadMapIfNeeded()
        {
            if (mapLoadingSource == null) return;
            MemoryStream ms = new MemoryStream();
            _gameSaver.Save(ms);
            mapLoadingSource.TrySetResult(ms.ToArray());
            mapLoadingSource = null;
        }

        public void PostLoad()
        {
            // TODO: have a UI :D
            if (EventIO.Config.GetNetMode() != NetMode.Server) return;
            Start();
        }

        public void UpdateSingleton()
        {
            if (isLoadingMap)
            {
                isLoadingMap = false;
                _replayService.FinishFullTickIfNeededAndThen(() => LoadMapIfNeeded());
            }
        }
    }
}
