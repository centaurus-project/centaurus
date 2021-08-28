using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderRequestProcessor : RequestQuantumProcessor
    {
        public OrderRequestProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(OrderRequest).Name;

        public override Task<QuantumResultMessageBase> Process(RequestContext context)
        {
            context.UpdateNonce();

            context.CentaurusContext.Exchange.ExecuteOrder(context);

            return Task.FromResult((QuantumResultMessageBase)context.Quantum.CreateEnvelope<MessageEnvelopeSigneless>().CreateResult(ResultStatusCodes.Success));
        }

        private int MaxCrossOrdersCount = 100;

        public override Task Validate(RequestContext context)
        {
            context.ValidateNonce();

            var quantum = context.Request;
            var orderRequest = quantum.RequestEnvelope.Message as OrderRequest;
            var baseAsset = context.CentaurusContext.Constellation.QuoteAsset;

            if (baseAsset.Code == orderRequest.Asset)
                throw new BadRequestException("Order asset must be different from quote asset.");

            var orderAsset = context.CentaurusContext.Constellation.Assets.FirstOrDefault(a => a.Code == orderRequest.Asset);
            if (orderAsset == null)
                throw new BadRequestException("Invalid asset identifier: " + orderRequest.Asset);

            //estimate XLM amount
            var quoteAmount = OrderMatcher.EstimateQuoteAmount(orderRequest.Amount, orderRequest.Price, orderRequest.Side);

            //check that lot size is greater than minimum allowed lot
            if (quoteAmount < context.CentaurusContext.Constellation.MinAllowedLotSize)
                throw new BadRequestException("Lot size is smaller than the minimum allowed lot.");

            //check required balances
            if (orderRequest.Side == OrderSide.Sell)
            {
                var balance = context.InitiatorAccount.GetBalance(orderRequest.Asset);
                if (!balance.HasSufficientBalance(orderRequest.Amount, 0))
                    throw new BadRequestException("Insufficient funds");
            }
            else
            {
                var balance = context.InitiatorAccount.GetBalance(baseAsset.Code);
                if (!balance.HasSufficientBalance(quoteAmount, context.CentaurusContext.Constellation.MinAccountBalance))
                    throw new BadRequestException("Insufficient funds");
            }

            ValidateCounterOrdersCount(orderRequest, context.CentaurusContext.Exchange.GetOrderbook(orderRequest.Asset, orderRequest.Side.Inverse()));

            return Task.CompletedTask;
        }


        //TODO: find more elegant and reliable way to validate cross orders count. This method is  could have rounding errors.
        /// <summary>
        /// Prevents too many trade effects.
        /// </summary>
        private void ValidateCounterOrdersCount(OrderRequest orderRequest, OrderbookBase orderbook)
        {
            var counterOrdersSum = 0ul;
            var counterOrdersCount = 0;
            foreach (var order in orderbook)
            {
                counterOrdersSum += order.Order.Amount;
                counterOrdersCount++;
                if (counterOrdersSum >= orderRequest.Amount)
                    break;
                if (counterOrdersCount > MaxCrossOrdersCount)
                    throw new BadRequestException("Failed to execute order. Maximum crossed orders length exceeded");
            }
        }
    }
}
