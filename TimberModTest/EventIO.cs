using System;
using System.Collections.Generic;
using System.Text;

namespace TimberModTest
{
    public interface EventIO
    {
        List<ReplayEvent> UpdateAndReadEvents();

        void WriteEvents(params ReplayEvent[] events);

        void Close();
    }
}
