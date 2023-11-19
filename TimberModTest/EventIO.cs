using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TimberModTest
{
    public interface EventIO
    {

        void Update();

        List<ReplayEvent> ReadEvents(int ticksSinceLoad);

        void WriteEvents(params ReplayEvent[] events);

        void Close();

        private static EventIO instance;
        public static EventIO Get() { return instance; }
        public static void Set(EventIO io) { instance = io; }
    }
}
