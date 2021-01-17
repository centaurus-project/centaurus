using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class MessageEnvelopeExtensions
    {

        public static void TryAssignAccountWrapper(this MessageEnvelope envelope)
        {
            var requestMessage = default(RequestMessage);
            if (envelope.Message is RequestQuantum)
                requestMessage = ((RequestQuantum)envelope.Message).RequestMessage;
            else if (envelope.Message is RequestMessage)
                requestMessage = (RequestMessage)envelope.Message;
            else
                return;

            requestMessage.AccountWrapper = Global.AccountStorage.GetAccount(requestMessage.Account);
        }
    }
}
