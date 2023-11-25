using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using TimberNet;

namespace TimberModTest
{
    class ServerConnectionService : IPostLoadableSingleton, IUpdatableSingleton
    {
        private ServerEventIO io;

        private GameSaver _gaveSaver;

        private TaskCompletionSource<byte[]> mapLodingSource;

        public ServerConnectionService(GameSaver gameSaver)
        {
            _gaveSaver = gameSaver;

        }

        public void Start()
        {
            try
            {
                io = new ServerEventIO(25565, ProvideGameState());
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
                if (mapLodingSource == null)
                {
                    mapLodingSource = new TaskCompletionSource<byte[]>();
                }
                return mapLodingSource.Task;
            };
        }

        private void LoadMapIfNeeded()
        {
            if (mapLodingSource == null) return;
            MemoryStream ms = new MemoryStream();
            _gaveSaver.Save(ms);
            mapLodingSource.TrySetResult(ms.ToArray());
            mapLodingSource = null;
        }

        public void PostLoad()
        {
            // TODO: have a UI :D
            Start();
        }

        public void UpdateSingleton()
        {
            LoadMapIfNeeded();
        }
    }
}
