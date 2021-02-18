using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

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
            resultMessage.Balances = new List<Balance>();
            foreach (var balance in account.Balances)
            {
                resultMessage.Balances.Add(new Balance { Amount = balance.Amount, Asset = balance.Asset, Liabilities = balance.Liabilities });
            }
            //TODO: create property in Account object
            resultMessage.Orders = new List<Order>();
            foreach (var order in Global.Exchange.OrderMap.GetAllAccountOrders(account.Id))
            {
                resultMessage.Orders.Add(new Order { Account = order.Account, Amount = order.Amount, QuoteAmount = order.QuoteAmount, Price = order.Price, OrderId = order.OrderId });
            }

            return Task.FromResult((ResultMessage)resultMessage);
        }

        public override Task Validate(ProcessorContext context)
        {
            context.ValidateNonce();
            return Task.CompletedTask;
        }
    }
}
