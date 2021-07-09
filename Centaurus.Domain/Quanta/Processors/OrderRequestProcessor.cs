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
        public override MessageTypes SupportedMessageType => MessageTypes.OrderRequest;

        public override Task<QuantumResultMessage> Process(RequestContext context)
        {
            var quantum = (RequestQuantum)context.Envelope.Message;

            context.UpdateNonce();

            context.CentaurusContext.Exchange.ExecuteOrder(context.EffectProcessors);

            return Task.FromResult((QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success));
        }

        private int MaxCrossOrdersCount = 100;

        public override Task Validate(RequestContext context)
        {
            context.ValidateNonce();

            var quantum = context.Envelope.Message as RequestQuantum;
            var orderRequest = quantum.RequestEnvelope.Message as OrderRequest;

            if (!context.CentaurusContext.Constellation.Assets.Any(a => a.Code == orderRequest.Asset))
                throw new InvalidOperationException("Invalid asset identifier: " + orderRequest.Asset);

            //estimate XLM amount
            var quoteAmount = OrderMatcher.EstimateQuoteAmount(orderRequest.Amount, orderRequest.Price, orderRequest.Side);

            //check that lot size is greater than minimum allowed lot
            if (quoteAmount < context.CentaurusContext.Constellation.MinAllowedLotSize)
                throw new BadRequestException("Lot size is smaller than the minimum allowed lot.");

            //check required balances
            if (orderRequest.Side == OrderSide.Sell)
            {
                var balance = context.SourceAccount.Account.GetBalance(orderRequest.Asset);
                if (!balance.HasSufficientBalance(orderRequest.Amount, 0))
                    throw new BadRequestException("Insufficient funds");
            }
            else
            {
                var baseAsset = context.CentaurusContext.Constellation.Assets.First();
                var balance = context.SourceAccount.Account.GetBalance(baseAsset.Code);
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
