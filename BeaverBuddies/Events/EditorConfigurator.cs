using BeaverBuddies.Editor;
using BeaverBuddies.MultiStart;
using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.StartingLocationSystem;
using Timberborn.TemplateSystem;

namespace BeaverBuddies.Events
{

    [Context("MapEditor")]
    public class EditorConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            MultiStartConfigurator.Configure(containerDefinition);
        }
    }
}
