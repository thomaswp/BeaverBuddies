using Timberborn.Common;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;

namespace TimberModTest
{
    // Hypothesis: This gets called at different frequencies depending on game speed
    // It gets called each tick, and I think the number of ticks per game frame depends on game speed
    // Answer: yes the number of entities ticked per update is proprotional to the update time (i.e. game speed / FPS) and the number of entities
    // So # of entities does not change tick rate. It's based just on game speed.
    // Essentially they've turned Unity's variable time delta system into a fixed number of partial updates
    // Rather than updating a specific number of times each frame depending on game speed, it updates a certain number
    // of buckets (objects), which can be a fractional number of game updates e.g. 2.5.

    public class TickWathcerService : ITickableSingleton
    {
        IDayNightCycle _dayNightCycle;
        public int TicksSinceLoad { get; private set; }
        // TODO: This does seem to work, but since the start time
        // doesn't appear to be an int value, we end up with some weird
        // float rounding nonsense and I don't know if the values are
        // going to end up discrete. Might want to use ticks instead.
        public float TotalTimeInFixedSecons
        {
            get { return _dayNightCycle.HoursPassedToday / 24 * 460; }
        }

        public TickWathcerService(IDayNightCycle dayNightCycle, EventBus eventBus)
        {
            _dayNightCycle = dayNightCycle;
            TicksSinceLoad = 0;
            //Plugin.Log("" + Time.fixedDeltaTime);

            eventBus.Register(this);
        }

        [OnEvent]
        public void OnGameLoaded(NewGameInitializedEvent e)
        {
            //ReplayService.UpdateInstance();
        }

        public void Tick()
        {
            TicksSinceLoad++;
            //if (TicksSinceLoad % 5 == 0)
            //{
            //    Plugin.Log($"Tick: {TicksSinceLoad}; Seconds Today: {_dayNightCycle.HoursPassedToday / 24 * 460} Hour: {_dayNightCycle.HoursPassedToday}; Partial Day: {_dayNightCycle.PartialDayNumber}");
            //}
            //ReplayService.UpdateInstance();
        }
    }
}
