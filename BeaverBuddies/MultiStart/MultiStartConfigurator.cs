using BeaverBuddies.Editor;
using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.StartingLocationSystem;
using Timberborn.TemplateSystem;

namespace BeaverBuddies.MultiStart
{
    public static class MultiStartConfigurator
    {
        private class TemplateModuleProvider : IProvider<TemplateModule>
        {
            public TemplateModule Get()
            {
                TemplateModule.Builder builder = new TemplateModule.Builder();
                builder.AddDecorator<StartingLocation, StartingLocationPlayer>();
                return builder.Build();
            }
        }

        public static void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<StartBuildingsService>().AsSingleton();
            containerDefinition.MultiBind<TemplateModule>().ToProvider<TemplateModuleProvider>().AsSingleton();
        }
    }
}
