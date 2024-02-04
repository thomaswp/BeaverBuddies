using System;
using System.IO;
using System.Threading.Tasks;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.NotificationSystemUI;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Connect
{
    class ServerConnectionService : IPostLoadableSingleton, IUpdatableSingleton
    {
        private GameSaver _gameSaver;
        private ReplayService _replayService;
        private TickingService _tickingService;
        private NotificationPanel _notificationPanel;

        private TaskCompletionSource<byte[]> mapLoadingSource;
        private int ticksAtMapLoad;

        private bool isLoadingMap = false;

        public ServerConnectionService(GameSaver gameSaver,
            ReplayService replayService,
            TickingService tickingService,
            EventBus eventBus,
            NotificationPanel notificationPanel
        )
        {
            _gameSaver = gameSaver;
            _replayService = replayService;
            _tickingService = tickingService;
            _notificationPanel = notificationPanel;
            eventBus.Register(this);
        }

        public void Start()
        {
            // Check if the current IO is a server, and if not return
            ServerEventIO io = EventIO.Get() as ServerEventIO;
            if (io == null) return;

            try
            {
                io.Start(EventIO.Config.Port,
                    ProvideGameState(),
                    () => ticksAtMapLoad);
            }
            catch (Exception e)
            {
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
            Start();
        }

        public void UpdateSingleton()
        {
            if (isLoadingMap)
            {
                isLoadingMap = false;
                // This is doable, but not a priority...
                //_notificationPanel.AddNotification(new Notification());
                _replayService.FinishFullTickIfNeededAndThen(() => LoadMapIfNeeded());
            }
        }
    }
}
