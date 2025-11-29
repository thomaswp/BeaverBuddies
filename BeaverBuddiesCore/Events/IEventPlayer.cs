using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Events
{
    internal interface IEventPlayer
    {
        void PlayEvent(MultiplayerEvent replayEvent);
    }
}
