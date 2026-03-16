using BeaverBuddies.MultiStart;
using Bindito.Core;

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
