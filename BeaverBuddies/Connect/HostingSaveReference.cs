using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.GameSaveRepositorySystem;

namespace BeaverBuddies.Connect
{
    internal class HostingSaveReference : SaveReference
    {
        public HostingSaveReference(string settlementName, string saveName) : base(settlementName, saveName)
        {
        }
    }
}
