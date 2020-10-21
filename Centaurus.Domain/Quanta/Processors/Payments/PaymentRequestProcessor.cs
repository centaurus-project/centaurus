using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class PaymentRequestProcessor : QuantumRequestProcessor<PaymentProcessorContext>
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.PaymentRequest;

        public override PaymentProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new PaymentProcessorContext(container);
        }

        public override Task<ResultMessage> Process(PaymentProcessorContext context)
        {
            context.UpdateNonce();

            var payment = context.Payment;

            if (!context.Destination.HasBalance(payment.Asset))
                context.EffectProcessors.AddBalanceCreate(context.Destination, payment.Asset);
            context.EffectProcessors.AddBalanceUpdate(context.Destination, payment.Asset, payment.Amount);

            context.EffectProcessors.AddBalanceUpdate(context.SourceAccount, payment.Asset, -payment.Amount);
            var effects = context.EffectProcessors.GetEffects();

            var accountEffects = effects.Where(e => ByteArrayPrimitives.Equals(e.Pubkey, payment.Account)).ToList();
            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        public override Task Validate(PaymentProcessorContext context)
        {
            context.ValidateNonce();

            var payment = context.Payment;

            if (payment.Account == null || payment.Account.IsZero())
                throw new BadRequestException("Source should be valid public key");

            if (payment.Destination == null || payment.Destination.IsZero())
                throw new BadRequestException("Destination should be valid public key");

            if (context.SourceAccount == null)
                throw new BadRequestException("No destination account.");

            if (payment.Destination.Equals(payment.Account))
                throw new BadRequestException("Source and destination must be different public keys");

            if (payment.Amount <= 0)
                throw new BadRequestException("Amount should be greater than 0");

            if (!Global.AssetIds.Contains(payment.Asset))
                throw new BadRequestException($"Asset {payment.Asset} is not supported");

            var account = payment.AccountWrapper.Account;
            if (account == null)
                throw new Exception("Quantum source has no account");

            var balance = account.Balances.Find(b => b.Asset == payment.Asset);
            if (balance == null || !balance.HasSufficientBalance(payment.Amount))
                throw new BadRequestException("Insufficient funds");

            return Task.CompletedTask;
        }
    }
}
