using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AccountDataRequestProcessor : QuantumProcessorBase
    {
        public AccountDataRequestProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(AccountDataRequest).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem quantumProcessingItem)
        {
            var quantum = (AccountDataRequestQuantum)quantumProcessingItem.Quantum;
            var requestMessage = quantum.RequestMessage;

            UpdateNonce(quantumProcessingItem);

            var resultMessage = (AccountDataResponse)quantumProcessingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success);
            resultMessage.Balances = quantumProcessingItem.Initiator.Balances
                .Values
                .Select(balance => new Balance { Amount = balance.Amount, Asset = balance.Asset, Liabilities = balance.Liabilities })
                .OrderBy(balance => balance.Asset)
                .ToList();

            resultMessage.Orders = quantumProcessingItem.Initiator.Orders
                .Values
                .Select(order =>
                    new Order
                    {
                        Amount = order.Amount,
                        QuoteAmount = order.QuoteAmount,
                        Price = order.Price,
                        OrderId = order.OrderId,
                        Asset = order.Asset,
                        Side = order.Side
                    })
                .OrderBy(order => order.OrderId)
                .ToList();

            resultMessage.Sequence = quantumProcessingItem.Initiator.AccountSequence;

            quantum.PayloadHash = resultMessage.ComputePayloadHash();

            return Task.FromResult((QuantumResultMessageBase)resultMessage);
        }

        public override Task Validate(QuantumProcessingItem quantumProcessingItem)
        {
            ValidateNonce(quantumProcessingItem);
            return Task.CompletedTask;
        }
    }
}
