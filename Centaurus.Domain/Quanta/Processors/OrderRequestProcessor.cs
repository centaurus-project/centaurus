using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderRequestProcessor : QuantumProcessorBase
    {
        public OrderRequestProcessor(ExecutionContext context)
            :base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(OrderRequest).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem processingItem)
        {
            UpdateNonce(processingItem);

            var quantum = (ClientRequestQuantumBase)processingItem.Quantum;
            var orderRequest = (OrderRequest)quantum.RequestEnvelope.Message;

            Context.Exchange.ExecuteOrder(orderRequest.Asset, Context.ConstellationSettingsManager.Current.QuoteAsset.Code, processingItem);

            return Task.FromResult((QuantumResultMessageBase)quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success));
        }

        private int MaxCrossOrdersCount = 100;

        public override Task Validate(QuantumProcessingItem processingItem)
        {
            ValidateNonce(processingItem);

            var quantum = (ClientRequestQuantumBase)processingItem.Quantum;
            var orderRequest = (OrderRequest)quantum.RequestEnvelope.Message;
            var baseAsset = Context.ConstellationSettingsManager.Current.QuoteAsset;

            if (baseAsset.Code == orderRequest.Asset)
                throw new BadRequestException("Order asset must be different from quote asset.");

            var orderAsset = Context.ConstellationSettingsManager.Current.Assets.FirstOrDefault(a => a.Code == orderRequest.Asset);
            if (orderAsset == null || orderAsset.IsSuspended)
                throw new BadRequestException($"Asset {orderRequest.Asset} is not supported or suspended.");

            if (processingItem.Initiator.Orders.Any(o => o.Value.Asset == orderRequest.Asset && o.Value.Side != orderRequest.Side))
                throw new BadRequestException("You cannot place order that crosses own order.");

            //estimate XLM amount
            var quoteAmount = OrderMatcher.EstimateQuoteAmount(orderRequest.Amount, orderRequest.Price, orderRequest.Side);

            //check that lot size is greater than minimum allowed lot
            if (quoteAmount < Context.ConstellationSettingsManager.Current.MinAllowedLotSize)
                throw new BadRequestException("Lot size is smaller than the minimum allowed lot.");

            //check required balances
            if (orderRequest.Side == OrderSide.Sell)
            {
                var balance = processingItem.Initiator.GetBalance(orderRequest.Asset);
                if (!balance.HasSufficientBalance(orderRequest.Amount, 0))
                    throw new BadRequestException("Insufficient funds");
            }
            else
            {
                var balance = processingItem.Initiator.GetBalance(baseAsset.Code);
                if (!balance.HasSufficientBalance(quoteAmount, Context.ConstellationSettingsManager.Current.MinAccountBalance))
                    throw new BadRequestException("Insufficient funds");
            }

            ValidateCounterOrdersCount(orderRequest, Context.Exchange.GetOrderbook(orderRequest.Asset, orderRequest.Side.Inverse()));

            return Task.CompletedTask;
        }


        //TODO: find more elegant and reliable way to validate cross orders count. This method is  could have rounding errors.
        /// <summary>
        /// Prevents too many trade effects.
        /// </summary>
        private void ValidateCounterOrdersCount(OrderRequest orderRequest, Orderbook orderbook)
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
                    throw new BadRequestException("Failed to execute order. Maximum crossed orders length exceeded.");
            }
        }
    }
}
