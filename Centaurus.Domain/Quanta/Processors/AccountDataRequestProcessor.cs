﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AccountDataRequestProcessor : ClientRequestProcessorBase
    {
        public override MessageTypes SupportedMessageType => MessageTypes.AccountDataRequest;

        public override Task<ResultMessage> Process(MessageEnvelope envelope, EffectProcessorsContainer effectsContainer)
        {
            var quantum = (RequestQuantum)envelope.Message;
            var requestMessage = quantum.RequestMessage;

            UpdateNonce(effectsContainer);

            var accountEffects = effectsContainer.GetEffects(requestMessage.Account).ToList();

            var account = requestMessage.AccountWrapper.Account;

            var resultMessage = envelope.CreateResult<AccountDataResponse>(ResultStatusCodes.Success, accountEffects);
            resultMessage.Balances = account.Balances;
            resultMessage.Orders = Global.Exchange.OrderMap.GetAllAccountOrders(account).ToList();

            return Task.FromResult((ResultMessage)resultMessage);
        }

        public override Task Validate(MessageEnvelope envelope)
        {
            ValidateNonce(envelope);
            return Task.CompletedTask;
        }
    }
}
