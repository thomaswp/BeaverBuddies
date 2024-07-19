using BeaverBuddies.Connect;
using Bindito.Core;
using UnityEngine;

namespace Mods.HelloWorld.Scripts
{
    [Context("MainMenu")]
    public class HelloWorldConfigurator : IConfigurator
    {

        public void Configure(IContainerDefinition containerDefinition)
        {
            Debug.Log("Configurator!");
            containerDefinition.Bind<FirstTimerService>().AsSingleton();
        }

    }
}