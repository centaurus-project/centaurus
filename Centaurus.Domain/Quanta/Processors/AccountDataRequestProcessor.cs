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

        public override string SupportedMessageType { get; } = typeof(AccountDataRequest).Name;

        public override Task<QuantumResultMessageBase> Process(RequestContext context)
        {
            var quantum = (AccountDataRequestQuantum)context.Request;
            var requestMessage = quantum.RequestMessage;

            context.UpdateNonce();

            var resultMessage = (AccountDataResponse)context.Quantum.CreateEnvelope<MessageEnvelopeSigneless>().CreateResult(ResultStatusCodes.Success);
            resultMessage.Balances = context.InitiatorAccount.Account.Balances
                .Select(balance => new Balance { Amount = balance.Amount, Asset = balance.Asset, Liabilities = balance.Liabilities })
                .OrderBy(balance => balance.Asset)
                .ToList();

            resultMessage.Orders = context.CentaurusContext.Exchange.OrderMap.GetAllAccountOrders(context.InitiatorAccount)
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

            resultMessage.Sequence = context.InitiatorAccount.Account.AccountSequence;

            quantum.PayloadHash = resultMessage.ComputePayloadHash();

            return Task.FromResult((QuantumResultMessageBase)resultMessage);
        }

        public override Task Validate(RequestContext context)
        {
            context.ValidateNonce();
            return Task.CompletedTask;
        }
    }
}
