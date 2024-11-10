using BeaverBuddies.MultiStart;
using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.StartingLocationSystem;
using Timberborn.TemplateSystem;

namespace BeaverBuddies.Editor
{

    [Context("MapEditor")]
    public class EditorConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            SingletonManager.Reset();
            containerDefinition.Bind<StartingLocationNumberService>().AsSingleton();
            MultiStartConfigurator.Configure(containerDefinition);
        }
    }
}
