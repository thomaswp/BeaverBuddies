using BeaverBuddies.Editor;
using Bindito.Core;
using Timberborn.StartingLocationSystem;
using Timberborn.TemplateInstantiation;

namespace BeaverBuddies.MultiStart
{
    public static class MultiStartConfigurator
    {
        private class TemplateModuleProvider : IProvider<TemplateModule>
        {
            public TemplateModule Get()
            {
                TemplateModule.Builder builder = new TemplateModule.Builder();
                builder.AddDecorator<StartingLocationSpec, StartingLocationPlayer>();
                return builder.Build();
            }
        }

        public static void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<StartBuildingsService>().AsSingleton();
            containerDefinition.Bind<StartingLocationPlayer>().AsTransient();
            containerDefinition.MultiBind<TemplateModule>().ToProvider<TemplateModuleProvider>().AsSingleton();
        }
    }
}
