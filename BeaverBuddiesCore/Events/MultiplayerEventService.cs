using System;
using System.Linq;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Events
{
    public class MultiplayerEventService
    {
        private IReplayContext replayContext;
        private IEventPlayer eventPlayer;

        /// <summary>
        /// Singleton service to play multiplayer events.
        /// </summary>
        /// <param name="singletonRepository"></param>
        public MultiplayerEventService(ISingletonRepository singletonRepository)
        {
            DefaultEventPlayer defaultPlayer = new DefaultEventPlayer(singletonRepository);
            SetContextAndPlayer(defaultPlayer, defaultPlayer);
        }

        internal void SetContextAndPlayer(IReplayContext context, IEventPlayer player)
        {
            replayContext = context;
            eventPlayer = player;
        }

        /// <summary>
        /// Registers a singleton instance to be used during event replay, which
        /// can be accessed via the IReplayContext.GetSingleton method.
        /// </summary>
        /// <param name="instance"></param>
        public void RegisterSingleton(object instance)
        {
            replayContext.RegisterSingleton(instance);
        }

        /// <summary>
        /// Plays a multiplayer event using the configured event player.
        /// When not in a multiplayer session, the event will be immediately replayed.
        /// In a multiplayer session, the event will be synchronized across clients before replaying.
        /// </summary>
        /// <param name="multiplayerEvent"></param>
        public void PlayEvent(MultiplayerEvent multiplayerEvent)
        {
            eventPlayer.PlayEvent(multiplayerEvent);
        }

        public void PlayEvent<T>(string type, T parameters, Action<T> replay)
        {
            var multiplayerEvent = new ParameterizedMultiplayerEvent<T>(type, parameters, replay);
            eventPlayer.PlayEvent(multiplayerEvent);
        }
    }
}
