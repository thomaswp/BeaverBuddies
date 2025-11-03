using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Common;
using Timberborn.GameSceneLoading;

namespace BeaverBuddies.MultiStart
{
    public record MultiplayerNewGameModeSpec : NewGameModeSpec
    {
        public int Players { get; init;  }

        public MultiplayerNewGameModeSpec(NewGameModeSpec mode, int players) : base(mode)
        {
            Players = players;
        }
    }
}
