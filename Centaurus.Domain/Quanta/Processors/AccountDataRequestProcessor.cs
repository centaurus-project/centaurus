using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AccountDataRequestProcessor : RequestQuantumProcessor
    {
        public AccountDataRequestProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override MessageTypes SupportedMessageType => MessageTypes.AccountDataRequest;

        public override Task<QuantumResultMessage> Process(RequestContext context)
        {
            var quantum = context.Request;
            var requestMessage = quantum.RequestMessage;

            context.UpdateNonce();

            var resultMessage = (AccountDataResponse)context.QuantumEnvelope.CreateResult(ResultStatusCodes.Success);
            resultMessage.Balances = context.SourceAccount.Account.Balances
                .Select(balance => new Balance { Amount = balance.Amount, Asset = balance.Asset, Liabilities = balance.Liabilities })
                .OrderBy(balance => balance.Asset)
                .ToList();

            resultMessage.Orders = context.CentaurusContext.Exchange.OrderMap.GetAllAccountOrders(context.SourceAccount)
                .Select(order =>
                    new Order
                    {
                        Amount = order.Order.Amount,
                        QuoteAmount = order.Order.QuoteAmount,
                        Price = order.Order.Price,
                        OrderId = order.OrderId,
                        Asset = order.Order.Asset,
                        Side = order.Order.Side
                    })
                .OrderBy(order => order.OrderId)
                .ToList();



            return Task.FromResult((QuantumResultMessage)resultMessage);
        }

        public override Task Validate(RequestContext context)
        {
            context.ValidateNonce();
            return Task.CompletedTask;
        }
    }
}
