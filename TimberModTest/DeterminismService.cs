using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;

namespace TimberModTest
{
    public class DeterminismService
    {
        EventBus _eventBus;

        DeterminismService(EventBus eventBus, IRandomNumberGenerator gen)
        {
            _eventBus = eventBus;
            Plugin.Log($"Creating test service {eventBus}");
            eventBus.Register(this);
            UnityEngine.Random.InitState(1234);
            Plugin.Log($"Hopefully deterministic random number {gen.Range(0, 100)}");
        }

        [OnEvent]
        public void OnSpeedEvent(CurrentSpeedChangedEvent e)
        {
            Plugin.Log($"Speed changed to: {e.CurrentSpeed}; random reset");
            UnityEngine.Random.InitState(1234);
            //Plugin.Log("All Game events:");
            //foreach (var key in _eventBus._subscriptions._subscriptions.Keys)
            //{
            //    Plugin.Log($"EventBus subscription: {key.Name}");
            //}
        }

        //[OnEvent]
        //public void OnStartEvent(Event e)
        //{
        //    Plugin.Log($"Speed changed to: {e.CurrentSpeed}; random reset");
        //    UnityEngine.Random.InitState(1234);
        //}
    }


}
