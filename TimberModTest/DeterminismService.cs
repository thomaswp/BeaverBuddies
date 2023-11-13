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
            eventBus.Register(this);
        }

        // TODO: For some reason this is still necessary. I don't know if it
        // works because of the first time (which happens before PostLoad)
        // or the second time (which happens after PostLoad). Could be either
        // depending on when the first random thing happens.
        // Simple idea: ignore this if tick > 0
        [OnEvent]
        public void OnSpeedEvent(CurrentSpeedChangedEvent e)
        {
            Plugin.Log($"Speed changed to: {e.CurrentSpeed}; random reset");
            UnityEngine.Random.InitState(1234);
        }
    }


}
