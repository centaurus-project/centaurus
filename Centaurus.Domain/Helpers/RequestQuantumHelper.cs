using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class RequestQuantumHelper
    {
        /// <summary>
        /// Wraps client requests to corresponding quantum type
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        public static RequestQuantumBase GetQuantum(MessageEnvelopeBase envelope)
        {
            switch(envelope.Message)
            {
                case AccountDataRequest _:
                    return new AccountDataRequestQuantum { RequestEnvelope = envelope };
                case WithdrawalRequest _:
                    return new WithdrawalRequestQuantum { RequestEnvelope = envelope };
                default:
                    if (envelope.Message is SequentialRequestMessage)
                        return new RequestQuantum { RequestEnvelope = envelope };
                    else
                        throw new Exception($"{envelope.Message.GetMessageType()} is not request quantum message.");
            }
        }
    }
}
