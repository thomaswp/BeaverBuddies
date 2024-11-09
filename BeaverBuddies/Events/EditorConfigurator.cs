using BeaverBuddies.Editor;
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
            containerDefinition.Bind<StartingLocationNumbererService>().AsSingleton();
            //containerDefinition.MultiBind<TemplateModule>().ToProvider<TemplateModuleProvider>().AsSingleton();
        }

        private class TemplateModuleProvider : IProvider<TemplateModule>
        {
            public TemplateModule Get()
            {
                TemplateModule.Builder builder = new TemplateModule.Builder();
                builder.AddDecorator<StartingLocation, StartingLocationPlayer>();
                return builder.Build();
            }
        }
    }
}
