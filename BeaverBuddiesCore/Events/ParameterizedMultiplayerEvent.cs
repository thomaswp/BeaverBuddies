using System;
using System.Collections.Generic;
using System.Text;

namespace BeaverBuddies.Events
{
    public class ParameterizedMultiplayerEvent : MultiplayerEvent
    {
        private string _type;
        private object _parameters;

        public override string type => _type;

        public ParameterizedMultiplayerEvent(string type, object parameters)
        {
            _parameters = parameters;
            _type = type;
        }

        // TODO: Problem: the method can't be a lambda because it needs to be something
        // we can find in the deserialization process... Honestly named types may be the best way here...
        public override void Replay(IReplayContext context)
        {
            throw new NotImplementedException();
        }
    }
}
