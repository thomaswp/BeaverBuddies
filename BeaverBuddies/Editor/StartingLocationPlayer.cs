using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;

namespace BeaverBuddies.Editor
{
    public class StartingLocationPlayer : BaseComponent, IRegisteredComponent
    {
        public int PlayerIndex { get; set; }
    }
}
