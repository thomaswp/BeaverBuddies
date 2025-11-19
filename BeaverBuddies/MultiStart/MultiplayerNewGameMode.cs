using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Common;
using Timberborn.GameSceneLoading;
using Timberborn.NewGameConfigurationSystem;

namespace BeaverBuddies.MultiStart
{
    public record MultiplayerNewGameModeSpec : GameModeSpec
    {
        public int Players { get; init; }

        public MultiplayerNewGameModeSpec(GameModeSpec mode, int players) : base(mode)
        {
            Players = players;
        }
    }
}
