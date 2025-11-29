using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Common;

namespace BeaverBuddies.Determinism
{
    public class TickLogicRandomService
    {
        internal IRandomNumberGenerator TickLogicRandomNumberGenerator { get; set; }

        public TickLogicRandomService(IRandomNumberGenerator randomNumberGenerator)
        {
            // Will be replaced when running in multiplayer with a deterministic RNG
            TickLogicRandomNumberGenerator = randomNumberGenerator;
        }

        public IRandomNumberGenerator GetTickLogicRNG()
        {
            return TickLogicRandomNumberGenerator;
        }
    }
}
