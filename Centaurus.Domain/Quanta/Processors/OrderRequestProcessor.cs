using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderRequestProcessor : QuantumRequestProcessor
    {
        public override MessageTypes SupportedMessageType => MessageTypes.OrderRequest;

        public override Task<ResultMessage> Process(ProcessorContext context)
        {
            var quantum = (RequestQuantum)context.Envelope.Message;
            var requestMessage = quantum.RequestMessage;

            context.UpdateNonce();

            context.CentaurusContext.Exchange.ExecuteOrder(context.EffectProcessors);

            var accountEffects = context.EffectProcessors.GetEffects(requestMessage.Account).ToList();

            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        private int MaxCrossOrdersCount = 100;

        public override Task Validate(ProcessorContext context)
        {
            context.ValidateNonce();

            var quantum = context.Envelope.Message as RequestQuantum;
            var orderRequest = quantum.RequestEnvelope.Message as OrderRequest;

            if (orderRequest.Asset <= 0 || !context.CentaurusContext.AssetIds.Contains(orderRequest.Asset))
                throw new InvalidOperationException("Invalid asset identifier: " + orderRequest.Asset);

            //estimate XLM amount
            var quoteAmount = OrderMatcher.EstimateQuoteAmount(orderRequest.Amount, orderRequest.Price, orderRequest.Side);

            //check that lot size is greater than minimum allowed lot
            if (quoteAmount < context.CentaurusContext.Constellation.MinAllowedLotSize)
                throw new BadRequestException("Lot size is smaller than the minimum allowed lot.");

            //fetch user's account record
            var account = orderRequest.AccountWrapper.Account;

            //check required balances
            if (orderRequest.Side == OrderSide.Sell)
            {
                var balance = account.GetBalance(orderRequest.Asset);
                if (!balance.HasSufficientBalance(orderRequest.Amount))
                    throw new BadRequestException("Insufficient funds");
            }
            else
            {
                var balance = account.GetBalance(0);
                if (!balance.HasSufficientBalance(quoteAmount))
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
            var counterOrdersSum = 0L;
            var counterOrdersCount = 0;
            foreach (var order in orderbook)
            {
                counterOrdersSum += order.Amount;
                counterOrdersCount++;
                if (counterOrdersSum >= orderRequest.Amount)
                    break;
                if (counterOrdersCount > MaxCrossOrdersCount)
                    throw new BadRequestException("Failed to execute order. Maximum crossed orders length exceeded");
            }
        }
    }
}
