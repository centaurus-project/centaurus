using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{
    public class PaymentCommitProcessor : QuantumProcessor<PaymentCommitProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.TxCommitQuantum;

        public override Task<QuantumResultMessage> Process(PaymentCommitProcessorContext context)
        {
            var ledgerQuantum = (PaymentCommitQuantum)context.Envelope.Message;
            var ledgerNotification = ledgerQuantum.Source;

            context.EffectProcessors.AddCursorUpdate(context.PaymentProvider.NotificationsManager, ledgerNotification.Cursor, context.PaymentProvider.Cursor);

            for (var i = 0; i < ledgerNotification.Items.Count; i++)
            {
                var payment = ledgerNotification.Items[i];

                switch (payment.Type)
                {
                    case PaymentTypes.Deposit:
                        ProcessDeposit(payment as Deposit, context);
                        break;
                    case PaymentTypes.Withdrawal:
                        ProcessWithdrawal(payment as Withdrawal, context);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported payment type");
                }
            }

            context.PaymentProvider.NotificationsManager.RemovePayment(ledgerNotification.Cursor);

            return Task.FromResult((QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(PaymentCommitProcessorContext context)
        {
            //TODO: validate type automatically based on the SupportedMessageType
            var paymentQuantum = context.Envelope.Message as PaymentCommitQuantum
                ?? throw new ArgumentException($"Unexpected message type. Only messages of type {typeof(PaymentCommitQuantum).FullName} are supported.");

            if (paymentQuantum.Source == null
                || !context.PaymentProvider.NotificationsManager.TryGetNextPayment(out var paymentNotificationWrapper) 
                || !ByteArrayPrimitives.Equals(paymentNotificationWrapper.Payment.ComputeHash(), paymentQuantum.Source.ComputeHash()))
                throw new InvalidOperationException("Unexpected tx notification.");

            foreach (var paymentItem in paymentNotificationWrapper.Payment.Items)
            {
                switch (paymentItem.Type)
                {
                    case PaymentTypes.Deposit:
                        ValidateDeposit(paymentItem as Deposit);
                        break;
                    case PaymentTypes.Withdrawal:
                        ValidateWithdrawal(paymentItem as Withdrawal, context);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported payment type: " + paymentItem.Type.ToString());
                }
            }

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
                //ignore registration with non-native asset or with amount that is less than MinAccountBalance
                if (deposit.Asset != 0 || deposit.Amount < context.CentaurusContext.Constellation.MinAccountBalance)
                    return;
                var accId = context.CentaurusContext.AccountStorage.NextAccountId;
                context.EffectProcessors.AddAccountCreate(context.CentaurusContext.AccountStorage, accId, deposit.Destination);
                account = context.CentaurusContext.AccountStorage.GetAccount(accId);
            }

            if (!account.Account.HasBalance(deposit.Asset))
            {
                context.EffectProcessors.AddBalanceCreate(account, deposit.Asset);
            }

            context.EffectProcessors.AddBalanceUpdate(account, deposit.Asset, deposit.Amount);
        }

        private void ValidateWithdrawal(Withdrawal withdrawalModel, PaymentCommitProcessorContext context)
        {
            if (withdrawalModel == null)
                throw new ArgumentNullException(nameof(withdrawalModel));

            var withdrawal = context.PaymentProvider.WithdrawalStorage.GetWithdrawal(withdrawalModel.TransactionHash);
            if (withdrawal == null)
                throw new InvalidOperationException($"Withdrawal with hash '{withdrawalModel.TransactionHash.ToHex().ToLower()}' is not found.");
            context.Withdrawals.Add(withdrawalModel, withdrawal);
        }

        private void ProcessWithdrawal(Withdrawal withdrawalModel, PaymentCommitProcessorContext context)
        {
            var withdrawal = context.Withdrawals[withdrawalModel];
            if (withdrawalModel.PaymentResult != PaymentResults.Success)
            {
                //TODO: we need to notify client that something went wrong
            }
            context.EffectProcessors.AddWithdrawalRemove(withdrawal, withdrawalModel.PaymentResult == PaymentResults.Success, context.PaymentProvider.WithdrawalStorage);
        }

        public override PaymentCommitProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new PaymentCommitProcessorContext(container);
        }
    }
}