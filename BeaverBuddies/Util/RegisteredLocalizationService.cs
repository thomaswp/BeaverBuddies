using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Localization;

namespace BeaverBuddies.Util
{
    public class RegisteredLocalizationService : RegisteredSingleton
    {
        public ILoc ILoc { get; private set; }

        public RegisteredLocalizationService(ILoc iloc)
        {
            ILoc = iloc;
        }

        public static string T(string key)
        {
            return SingletonManager.GetSingleton<RegisteredLocalizationService>().ILoc.T(key);
        }
    }
}
