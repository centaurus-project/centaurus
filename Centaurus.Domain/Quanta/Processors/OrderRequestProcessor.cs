using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderRequestProcessor : ClientRequestProcessorBase
    {
        public override MessageTypes SupportedMessageType => MessageTypes.OrderRequest;

        public override Task<ResultMessage> Process(MessageEnvelope envelope)
        {
            var quantum = (RequestQuantum)envelope.Message;
            var requestMessage = quantum.RequestMessage;

            var effectsContainer = new EffectProcessorsContainer(envelope, Global.AddEffects);

            UpdateNonce(effectsContainer);

            Global.Exchange.ExecuteOrder(effectsContainer);
            effectsContainer.Commit();

            var accountEffects = effectsContainer.GetEffects()
                .Where(e => e.Pubkey.Equals(requestMessage.Account))
                .ToList();

            return Task.FromResult(envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        //TODO: replace all system exceptions that occur on validation with our client exceptions
        public override Task Validate(MessageEnvelope envelope)
        {
            ValidateNonce(envelope);

            var quantum = envelope.Message as RequestQuantum;
            var orderRequest = (OrderRequest)quantum.RequestEnvelope.Message;

            if (orderRequest.Asset <= 0) throw new InvalidOperationException("Invalid asset for the orderbook: " + orderRequest.Asset);

            //estimate price in XLM
            var totalXlmAmountToTrade = OrderMatcher.EstimateTradedXlmAmount(orderRequest.Amount, orderRequest.Price);

            //check that lot size is greater than minimum allowed lot
            if (totalXlmAmountToTrade < Global.Constellation.MinAllowedLotSize) throw new BadRequestException("Lot size is smaller than the minimum allowed lot.");

            //fetch user's account record
            var account = Global.AccountStorage.GetAccount(orderRequest.Account);

            //check required balances
            if (orderRequest.Side == OrderSides.Sell)
            {
                var balance = account.GetBalance(orderRequest.Asset);
                if (!balance.HasSufficientBalance(orderRequest.Amount)) throw new BadRequestException("Insufficient funds");
            }
            else
            {
                var balance = account.GetBalance(0);
                if (!balance.HasSufficientBalance(totalXlmAmountToTrade)) throw new BadRequestException("Insufficient funds");
            }

            return Task.CompletedTask;
        }
    }
}
