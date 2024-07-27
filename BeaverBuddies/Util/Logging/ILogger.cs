using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Util.Logging
{
    internal interface ILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}
