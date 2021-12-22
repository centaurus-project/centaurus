using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class DepositProcessor : QuantumProcessorBase
    {
        public DepositProcessor(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(DepositQuantum).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem quantumProcessingItem)
        {
            var depositQuantum = (DepositQuantum)quantumProcessingItem.Quantum;
            var depositNotification = depositQuantum.Source;

            if (!Context.PaymentProvidersManager.TryGetManager(depositNotification.Provider, out var paymentProvider))
                throw new Exception($"Payment provider {paymentProvider} is not registered.");

            if (depositQuantum.Source == null
                || !TryGetNotification(paymentProvider, out var notification)
                || !ByteArrayPrimitives.Equals(notification.ComputeHash(), depositQuantum.Source.ComputeHash()))
            {
                throw new InvalidOperationException("Unexpected tx notification.");
            }

            quantumProcessingItem.AddCursorUpdate(paymentProvider.NotificationsManager, depositNotification.Provider, depositNotification.Cursor, paymentProvider.Cursor);

            foreach (var payment in depositNotification.Items)
                ProcessDeposit(payment, quantumProcessingItem);

            paymentProvider.NotificationsManager.RemoveNextNotification();

            return Task.FromResult((QuantumResultMessageBase)quantumProcessingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success));
        }

        public override Task Validate(QuantumProcessingItem quantumProcessingItem)
        {
            //no validations needed. If the quantum is invalid than there is a problem with Alpha
            return Task.CompletedTask;
        }

        private bool TryGetNotification(PaymentProviderBase paymentProvider, out DepositNotification notification)
        {
            notification = null;
            if (!paymentProvider.NotificationsManager.TryGetNextNotification(out var paymentNotification))
                return false;
            notification = paymentNotification.ToDomainModel();
            return true;
        }

        /// <summary>
        /// Creates balance and account if needed, updates balance
        /// </summary>
        private void ProcessDeposit(Deposit deposit, QuantumProcessingItem quantumProcessingItem)
        {
            if (deposit.PaymentResult == PaymentResults.Failed)
                return;

            var account = Context.AccountStorage.GetAccount(deposit.Destination);
            if (account == null)
            {
                var baseAsset = Context.ConstellationSettingsManager.Current.QuoteAsset.Code;
                //ignore registration with non-base asset or with amount that is less than MinAccountBalance
                if (deposit.Asset != baseAsset || deposit.Amount < Context.ConstellationSettingsManager.Current.MinAccountBalance)
                    return;
                quantumProcessingItem.AddAccountCreate(Context.AccountStorage, deposit.Destination, Context.ConstellationSettingsManager.Current.RequestRateLimits);
                account = Context.AccountStorage.GetAccount(deposit.Destination);
            }

            if (!account.HasBalance(deposit.Asset))
                quantumProcessingItem.AddBalanceCreate(account, deposit.Asset);

            quantumProcessingItem.AddBalanceUpdate(account, deposit.Asset, deposit.Amount, UpdateSign.Plus);
        }
    }
}