using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class PaymentCommitProcessor : QuantumProcessorBase<PaymentCommitProcessorContext>
    {
        public PaymentCommitProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override MessageTypes SupportedMessageType => MessageTypes.DepositQuantum;

        public override Task<QuantumResultMessage> Process(PaymentCommitProcessorContext context)
        {
            var depositQuantum = (DepositQuantum)context.QuantumEnvelope.Message;
            var depositNotification = depositQuantum.Source;

            context.AddCursorUpdate(context.PaymentProvider.NotificationsManager, depositNotification.ProviderId, depositNotification.Cursor, context.PaymentProvider.Cursor);

            foreach (var payment in depositNotification.Items)
                ProcessDeposit(payment, context);

            context.PaymentProvider.NotificationsManager.RemovePayment(depositNotification.Cursor);

            return Task.FromResult((QuantumResultMessage)context.QuantumEnvelope.CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(PaymentCommitProcessorContext context)
        {
            //TODO: validate type automatically based on the SupportedMessageType
            var paymentQuantum = context.QuantumEnvelope.Message as DepositQuantum
                ?? throw new ArgumentException($"Unexpected message type. Only messages of type {typeof(DepositQuantum).FullName} are supported.");

            if (paymentQuantum.Source == null
                || !TryGetNotification(context, out var notification)
                || !ByteArrayPrimitives.Equals(notification.ComputeHash(), paymentQuantum.Source.ComputeHash()))
                throw new InvalidOperationException("Unexpected tx notification.");

            foreach (var paymentItem in notification.Items)
                ValidateDeposit(context, paymentItem);

            return Task.CompletedTask;
        }

        private bool TryGetNotification(PaymentCommitProcessorContext context, out DepositNotification notification)
        {
            notification = null;
            if (!context.PaymentProvider.NotificationsManager.TryGetNextPayment(out var paymentNotification))
                return false;
            notification = paymentNotification.ToDomainModel();
            return true;
        }

        private void ValidateDeposit(PaymentCommitProcessorContext context, Deposit deposit)
        {
            if (deposit == null)
                throw new ArgumentNullException(nameof(deposit));

            if (deposit.Destination == 0)
                throw new InvalidOperationException("Destination id is invalid.");

            if (context.CentaurusContext.AccountStorage.GetAccount(deposit.Destination) == null)
                throw new InvalidOperationException("Unknown destination.");

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
            //if (account == null)
            //{
            //    var baseAsset = context.CentaurusContext.Constellation.GetBaseAsset();
            //    //ignore registration with non-base asset or with amount that is less than MinAccountBalance
            //    if (deposit.Asset != baseAsset || deposit.Amount < context.CentaurusContext.Constellation.MinAccountBalance)
            //        return;
            //    var accId = context.CentaurusContext.AccountStorage.NextAccountId;
            //    context.AddAccountCreate(context.CentaurusContext.AccountStorage, accId, deposit.Destination);
            //    account = context.CentaurusContext.AccountStorage.GetAccount(accId);
            //}

            if (!account.Account.HasBalance(deposit.Asset))
                context.AddBalanceCreate(account, deposit.Asset);

            context.AddBalanceUpdate(account, deposit.Asset, deposit.Amount, UpdateSign.Plus);
        }

        public override ProcessorContext GetContext(MessageEnvelope messageEnvelope, AccountWrapper account)
        {
            return new PaymentCommitProcessorContext(Context, messageEnvelope, account);
        }
    }
}