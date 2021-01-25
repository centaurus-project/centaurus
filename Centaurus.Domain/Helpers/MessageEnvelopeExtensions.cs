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
            envelope.TryAssignAccountWrapper(Global.AccountStorage);
        }

        public static void TryAssignAccountWrapper(this MessageEnvelope envelope, AccountStorage accountStorage)
        {
            var requestMessage = default(RequestMessage);
            if (envelope.Message is RequestQuantum)
                requestMessage = ((RequestQuantum)envelope.Message).RequestMessage;
            else if (envelope.Message is RequestMessage)
                requestMessage = (RequestMessage)envelope.Message;
            else
                return;

            if (accountStorage == null)
                throw new ArgumentNullException(nameof(accountStorage));
            requestMessage.AccountWrapper = accountStorage.GetAccount(requestMessage.Account);
        }
    }
}
