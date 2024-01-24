using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timberborn.EntitySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.MapSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;
using TimberModTest.Events;
using TimberNet;

namespace TimberModTest
{
    class ServerConnectionService : IPostLoadableSingleton, IUpdatableSingleton
    {
        private ServerEventIO io;

        private GameSaver _gameSaver;
        private ReplayService _replayService;
        private TickingService _tickingService;

        private TaskCompletionSource<byte[]> mapLoadingSource;
        private int ticksAtMapLoad;

        private bool isLoadingMap = false;

        public ServerConnectionService(GameSaver gameSaver, ReplayService replayService, 
            TickingService tickingService)
        {
            _gameSaver = gameSaver;
            _replayService = replayService;
            _tickingService = tickingService;
        }

        public void Start()
        {
            try
            {
                io = new ServerEventIO(EventIO.Config.Port,
                    ProvideGameState(),
                    () => ticksAtMapLoad);
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
            byte[] bytes = ms.ToArray();
            //Plugin.Log($"Sending map with {bytes.Length} length");
            ticksAtMapLoad = _replayService.TicksSinceLoad;
            // Because the connecting client will not have yet
            // ticked the ReplayService when it connects, if the
            // server has, we rewind by one "frame" so it can be synced.
            // Note: this isn't a full frame. Both are at the start of a frame,
            // the only difference is whether or not the ReplayService has ticked
            // and advanced the frame count.
            if (_tickingService.HasTickedReplayService)
            {
                ticksAtMapLoad--;
            }
            else
            {
                _replayService.SetTargetSpeed(0);
            }

            // Unfortunately this seems to desync even on initial connects
            // Refresh walker paths, since the loaded game will have
            // freshly calculated paths
            //var entityService = _replayService.GetSingleton<EntityService>();
            //var entities = entityService._entityRegistry.Entities;
            //var walkers = entities.Select(e => e.GetComponentFast<Walker>()).Where(w => w != null);
            //foreach (Walker walker in walkers)
            //{
            //    walker.RefreshPath();
            //}

            mapLoadingSource.TrySetResult(bytes);
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
