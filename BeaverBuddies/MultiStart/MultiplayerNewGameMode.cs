using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Common;
using Timberborn.GameSceneLoading;

namespace BeaverBuddies.MultiStart
{
    internal class MultiplayerNewGameMode : NewGameMode
    {
        public int Players { get; }

        public MultiplayerNewGameMode(NewGameMode mode, int players) : 
            base(mode.StartingAdults, mode.AdultAgeProgress, mode.StartingChildren, mode.ChildAgeProgress, mode.FoodConsumption, mode.WaterConsumption, mode.StartingFood, mode.StartingWater, mode.TemperateWeatherDuration, mode.DroughtDuration, mode.DroughtDurationHandicapMultiplier, mode.DroughtDurationHandicapCycles, mode.CyclesBeforeRandomizingBadtide, mode.ChanceForBadtide, mode.BadtideDuration, mode.BadtideDurationHandicapMultiplier, mode.BadtideDurationHandicapCycles, mode.InjuryChance, mode.DemolishableRecoveryRate)
        {
            Players = players;
        }
    }
}
