using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Events
{
    public class DefaultEventPlayer : IEventPlayer, IReplayContext
    {

        private readonly ISingletonRepository _singletonRepository;
        private readonly Dictionary<Type, object> _cachedSingletons = new Dictionary<Type, object>();

        public DefaultEventPlayer(ISingletonRepository singletonRepository)
        {
            _singletonRepository = singletonRepository;
        }
        public void RegisterSingleton(object instance)
        {
            _cachedSingletons[instance.GetType()] = instance;
        }

        public T GetSingleton<T>()
        {
            if (_cachedSingletons.TryGetValue(typeof(T), out var instance))
            {
                return (T)instance;
            }

            var result = _singletonRepository.GetSingletons<T>().FirstOrDefault();
            return result;
        }

        public void PlayEvent(MultiplayerEvent replayEvent)
        {
            replayEvent.Replay(this);
        }
    }
}
