using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class PaymentCommitProcessor : QuantumProcessorBase<PaymentCommitProcessorContext>
    {
        public PaymentCommitProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(DepositQuantum).Name;

        public override Task<QuantumResultMessageBase> Process(PaymentCommitProcessorContext context)
        {
            var depositQuantum = (DepositQuantum)context.Quantum;
            var depositNotification = depositQuantum.Source;

            context.AddCursorUpdate(context.PaymentProvider.NotificationsManager, depositNotification.Provider, depositNotification.Cursor, context.PaymentProvider.Cursor);

            foreach (var payment in depositNotification.Items)
                ProcessDeposit(payment, context);

            context.PaymentProvider.NotificationsManager.RemoveNextNotification();

            return Task.FromResult((QuantumResultMessageBase)context.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success));
        }

        public override Task Validate(PaymentCommitProcessorContext context)
        {
            //TODO: validate type automatically based on the SupportedMessageType
            var paymentQuantum = context.Quantum as DepositQuantum
                ?? throw new ArgumentException($"Unexpected message type. Only messages of type {typeof(DepositQuantum).FullName} are supported.");

            if (paymentQuantum.Source == null
                || !TryGetNotification(context, out var notification)
                || !ByteArrayPrimitives.Equals(notification.ComputeHash(), paymentQuantum.Source.ComputeHash()))
            {
                throw new InvalidOperationException("Unexpected tx notification.");
            }

            foreach (var paymentItem in notification.Items)
                ValidateDeposit(context, paymentItem);

            return Task.CompletedTask;
        }

        private bool TryGetNotification(PaymentCommitProcessorContext context, out DepositNotification notification)
        {
            notification = null;
            if (!context.PaymentProvider.NotificationsManager.TryGetNextNotification(out var paymentNotification))
                return false;
            notification = paymentNotification.ToDomainModel();
            return true;
        }

        private void ValidateDeposit(PaymentCommitProcessorContext context, Deposit deposit)
        {
            if (deposit == null)
                throw new ArgumentNullException(nameof(deposit));

            if (deposit.Destination == null)
                throw new InvalidOperationException("Destination is invalid.");

            if (deposit.Amount <= 0)
                throw new InvalidOperationException("Amount should be greater than 0.");
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
                var baseAsset = context.CentaurusContext.Constellation.QuoteAsset.Code;
                //ignore registration with non-base asset or with amount that is less than MinAccountBalance
                if (deposit.Asset != baseAsset || deposit.Amount < context.CentaurusContext.Constellation.MinAccountBalance)
                    return;
                context.AddAccountCreate(context.CentaurusContext.AccountStorage, deposit.Destination);
                account = context.CentaurusContext.AccountStorage.GetAccount(deposit.Destination);
            }

            if (!account.HasBalance(deposit.Asset))
                context.AddBalanceCreate(account, deposit.Asset);

            context.AddBalanceUpdate(account, deposit.Asset, deposit.Amount, UpdateSign.Plus);
        }

        public override ProcessorContext GetContext(Quantum quantum, Account account)
        {
            return new PaymentCommitProcessorContext(Context, quantum, account);
        }
    }
}