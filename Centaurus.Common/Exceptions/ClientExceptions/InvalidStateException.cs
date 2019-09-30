using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class InvalidStateException: BaseClientException
    {
        public InvalidStateException(string messageType, string currentState, string[] validStates)
            : base($"Connection is in state {currentState}. Valid state(s) for the message of type '{messageType}' is(are) {string.Join(',', validStates)}")
        {
        }
    }
}
