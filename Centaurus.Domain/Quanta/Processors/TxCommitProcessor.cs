﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{
    public class TxCommitProcessor : QuantumRequestProcessor<LedgerCommitProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.TxCommitQuantum;

        public override Task<ResultMessage> Process(LedgerCommitProcessorContext context)
        {
            var ledgerQuantum = (TxCommitQuantum)context.Envelope.Message;
            var ledgerNotification = (TxNotification)ledgerQuantum.Source.Message;

            context.EffectProcessors.AddCursorUpdate(Global.TxCursorManager, ledgerNotification.TxCursor, Global.TxCursorManager.TxCursor);

            for (var i = 0; i < ledgerNotification.Payments.Count; i++)
            {
                var payment = ledgerNotification.Payments[i];

                switch (payment.Type)
                {
                    case PaymentTypes.Deposit:
                        ProcessDeposite(payment as Deposit, context);
                        break;
                    case PaymentTypes.Withdrawal:
                        ProcessWithdrawal(payment as Models.Withdrawal, context);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported payment type");
                }
            }

            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, context.EffectProcessors.Effects));
        }

        public override Task Validate(LedgerCommitProcessorContext context)
        {
            //TODO: validate type automatically based on the SupportedMessageType
            var ledgerQuantum = context.Envelope.Message as TxCommitQuantum
                ?? throw new ArgumentException($"Unexpected message type. Only messages of type {typeof(TxCommitQuantum).FullName} are supported.");

            var ledgerSourceEnvelope = ledgerQuantum.Source;
            var ledgerInfo = ledgerSourceEnvelope.Message as TxNotification
                ?? throw new ArgumentException($"Unexpected LedgerCommitQuantum source. Only messages of {typeof(TxNotification).FullName} type can be the source.");

            //no need to check signatures if code is running on Alpha, because the quantum is generated by it
            if (!Global.IsAlpha)
                CheckSignatures(ledgerSourceEnvelope);

            if (!Global.TxCursorManager.IsValidNewCursor(ledgerInfo.TxCursor))
                throw new InvalidOperationException($"Cursor is invalid. Current cursor is {Global.TxCursorManager.TxCursor} and received was {ledgerInfo.TxCursor}");

            foreach (var payment in ledgerInfo.Payments)
            {
                switch (payment.Type)
                {
                    case PaymentTypes.Deposit:
                        ValidateDeposite(payment as Deposit);
                        break;
                    case PaymentTypes.Withdrawal:
                        ValidateWithdrawal(payment as Models.Withdrawal, context);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported payment type: " + payment.Type.ToString());
                }
            }

            return Task.CompletedTask;
        }

        private void CheckSignatures(MessageEnvelope envelope)
        {
            if (!MajorityHelper.HasMajority(envelope))
                throw new InvalidOperationException("No majority");

            if (!envelope.AreSignaturesValid())
                throw new InvalidOperationException("Signatures is invalid");
        }

        private void ValidateDeposite(Deposit deposite)
        {
            if (deposite == null)
                throw new ArgumentNullException(nameof(deposite));

            if (deposite.Destination == null || deposite.Destination.IsZero())
                throw new InvalidOperationException("Destination should be valid public key");

            if (deposite.Amount <= 0)
                throw new InvalidOperationException("Amount should be greater than 0");
        }

        /// <summary>
        /// Creates balance and account if needed, updates balance
        /// </summary>
        private void ProcessDeposite(Deposit deposite, LedgerCommitProcessorContext context)
        {
            if (deposite.PaymentResult == PaymentResults.Failed)
                return;

            var account = Global.AccountStorage.GetAccount(deposite.Destination)?.Account;
            if (account == null)
            {
                var accId = Global.AccountStorage.GetNextAccountId();
                context.EffectProcessors.AddAccountCreate(Global.AccountStorage, accId, deposite.Destination);
                account = Global.AccountStorage.GetAccount(accId).Account;
            }

            if (!account.HasBalance(deposite.Asset))
            {
                context.EffectProcessors.AddBalanceCreate(account, deposite.Asset);
            }

            context.EffectProcessors.AddBalanceUpdate(account, deposite.Asset, deposite.Amount);
        }

        private void ValidateWithdrawal(Models.Withdrawal withdrawalModel, LedgerCommitProcessorContext context)
        {
            if (withdrawalModel == null)
                throw new ArgumentNullException(nameof(withdrawalModel));

            var withdrawal = Global.WithdrawalStorage.GetWithdrawal(withdrawalModel.TransactionHash);
            if (withdrawal == null)
                throw new InvalidOperationException($"Withdrawal with hash '{withdrawalModel.TransactionHash.ToHex().ToLower()}' is not found.");
            context.Withdrawals.Add(withdrawalModel, withdrawal);
        }

        private void ProcessWithdrawal(Models.Withdrawal withdrawalModel, LedgerCommitProcessorContext context)
        {
            var withdrawal = context.Withdrawals[withdrawalModel];
            var isSuccess = withdrawalModel.PaymentResult == PaymentResults.Success;
            foreach (var withdrawalItem in withdrawal.Withdrawals)
            {
                context.EffectProcessors.AddUpdateLiabilities(withdrawal.Source.Account, withdrawalItem.Asset, -withdrawalItem.Amount);
                if (isSuccess)
                    context.EffectProcessors.AddBalanceUpdate(withdrawal.Source.Account, withdrawalItem.Asset, -withdrawalItem.Amount);
            }
            if (!isSuccess)
            {
                //TODO: we need to notify client that something went wrong
            }
            context.EffectProcessors.AddWithdrawalRemove(withdrawal, Global.WithdrawalStorage);
        }

        public override LedgerCommitProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new LedgerCommitProcessorContext(container);
        }
    }
}
