using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Common;

namespace BeaverBuddies.Determinism
{
    [Context("Game")]
    public class DeterminismConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {

            containerDefinition.Bind<TickLogicRandomService>().AsSingleton();
        }
    }
}
