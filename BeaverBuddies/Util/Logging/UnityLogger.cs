using System;
using UnityEngine;

namespace BeaverBuddies.Util.Logging
{
    internal class UnityLogger : ILogger
    {
        public void LogInfo(string message)
        {
            Debug.Log(message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }
        public void LogError(string message)
        {
            Debug.LogError(message);
        }
    }
}
