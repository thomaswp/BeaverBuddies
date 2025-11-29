using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Events
{
    [Context("Game")]
    public class EventsConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<MultiplayerEventService>().AsSingleton();
        }
    }
}
