using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using Centaurus.Models.Extensions;

namespace Centaurus.Domain
{
    public class AccountDataRequestProcessor : QuantumRequestProcessor
    {
        public override MessageTypes SupportedMessageType => MessageTypes.AccountDataRequest;

        public override Task<ResultMessage> Process(ProcessorContext context)
        {
            var quantum = (RequestQuantum)context.Envelope.Message;
            var requestMessage = quantum.RequestMessage;

            context.UpdateNonce();

            var accountEffects = context.EffectProcessors.GetEffects(requestMessage.Account).ToList();

            var account = requestMessage.AccountWrapper.Account;

            var resultMessage = (AccountDataResponse)context.Envelope.CreateResult(ResultStatusCodes.Success, accountEffects);
            resultMessage.Balances = account.Balances.Select(b => b.Clone()).ToList();
            //TODO: create property in Account object
            resultMessage.Orders = Global.Exchange.OrderMap.GetAllAccountOrders(account).OrderBy(o => o.OrderId).Select(o => o.Clone()).ToList();

            return Task.FromResult((ResultMessage)resultMessage);
        }

        public override Task Validate(ProcessorContext context)
        {
            context.ValidateNonce();
            return Task.CompletedTask;
        }
    }
}
