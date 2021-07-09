using Centaurus.Models;
using System;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class PaymentCommitProcessor : QuantumProcessor<PaymentCommitProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.DepositQuantum;

        public override Task<QuantumResultMessage> Process(PaymentCommitProcessorContext context)
        {
            var depositQuantum = (DepositQuantum)context.Envelope.Message;
            var depositNotification = depositQuantum.Source;

            context.EffectProcessors.AddCursorUpdate(context.PaymentProvider.NotificationsManager, depositNotification.ProviderId, depositNotification.Cursor, context.PaymentProvider.Cursor);

            foreach (var payment in depositNotification.Items)
                ProcessDeposit(payment, context);

            context.PaymentProvider.NotificationsManager.RemovePayment(depositNotification.Cursor);

            return Task.FromResult((QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(PaymentCommitProcessorContext context)
        {
            //TODO: validate type automatically based on the SupportedMessageType
            var paymentQuantum = context.Envelope.Message as DepositQuantum
                ?? throw new ArgumentException($"Unexpected message type. Only messages of type {typeof(DepositQuantum).FullName} are supported.");

            if (paymentQuantum.Source == null
                || !context.PaymentProvider.NotificationsManager.TryGetNextPayment(out var paymentNotificationWrapper)
                || !ByteArrayPrimitives.Equals(paymentNotificationWrapper.Deposite.ComputeHash(), paymentQuantum.Source.ComputeHash()))
                throw new InvalidOperationException("Unexpected tx notification.");

            foreach (var paymentItem in paymentNotificationWrapper.Deposite.Items)
                ValidateDeposit(paymentItem);

            return Task.CompletedTask;
        }

        private void ValidateDeposit(Deposit deposit)
        {
            if (deposit == null)
                throw new ArgumentNullException(nameof(deposit));

            if (deposit.Destination == null || deposit.Destination.IsZero())
                throw new InvalidOperationException("Destination should be valid public key");

            if (deposit.Amount <= 0)
                throw new InvalidOperationException("Amount should be greater than 0");
        }

        /// <summary>
        /// Creates balance and account if needed, updates balance
        /// </summary>
        private void ProcessDeposit(Deposit deposit, PaymentCommitProcessorContext context)
        {
            if (deposit.PaymentResult == PaymentResults.Failed)
                return;

            var account = context.CentaurusContext.AccountStorage.GetAccount(deposit.Destination);
            if (account == null)
            {
                var baseAsset = context.CentaurusContext.Constellation.GetBaseAsset();
                //ignore registration with non-base asset or with amount that is less than MinAccountBalance
                if (deposit.Asset != baseAsset || deposit.Amount < context.CentaurusContext.Constellation.MinAccountBalance)
                    return;
                var accId = context.CentaurusContext.AccountStorage.NextAccountId;
                context.EffectProcessors.AddAccountCreate(context.CentaurusContext.AccountStorage, accId, deposit.Destination);
                account = context.CentaurusContext.AccountStorage.GetAccount(accId);
            }

            if (!account.Account.HasBalance(deposit.Asset))
                context.EffectProcessors.AddBalanceCreate(account, deposit.Asset);

            context.EffectProcessors.AddBalanceUpdate(account, deposit.Asset, deposit.Amount, UpdateSign.Plus);
        }

        public override PaymentCommitProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new PaymentCommitProcessorContext(container);
        }
    }
}