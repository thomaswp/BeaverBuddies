using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Steam
{
    public interface ISteamPacketReceiver
    {
        void RegisterSteamPacketListener(SteamPacketListener listener);
    }
}
