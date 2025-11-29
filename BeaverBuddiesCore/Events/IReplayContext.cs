using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Events
{
    public interface IReplayContext
    {
        /// <summary>
        /// Gets a registered singleton instance of the given type.
        /// Use singletons to replay events.
        /// Register singletons with <see cref="MultiplayerEventService.RegisterSingleton(object)"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetSingleton<T>();

        /// <summary>
        /// Registers a singleton to be used during event replay.
        /// Do not call this method directly; use <see cref="MultiplayerEventService.RegisterSingleton(object)"/> instead.
        /// </summary>
        /// <param name="instance"></param>
        void RegisterSingleton(object instance);
    }
}
