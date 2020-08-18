﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{
    public abstract class PaymentRequestProcessorBase : ClientRequestProcessorBase
    {
        public override Task<ResultMessage> Process(MessageEnvelope envelope, EffectProcessorsContainer effectsContainer)
        {
            UpdateNonce(effectsContainer);

            var requestQuantum = (RequestQuantum)envelope.Message;

            var payment = (PaymentRequestBase)requestQuantum.RequestEnvelope.Message;

            var paymentAccount = payment.AccountWrapper.Account;

            AccountData vaultAccount = Global.VaultAccount;

            effectsContainer.AddLockLiabilities(paymentAccount, payment.Asset, payment.Amount);
            var destAccount = Global.AccountStorage.GetAccount(payment.Destination);

            //if withdrawal requested or if account doesn't exist in Centaurus, we need to build transaction
            if (payment.MessageType == MessageTypes.WithdrawalRequest || destAccount == null)
            {
                var withdrawal = new Withdrawal
                {
                    Apex = requestQuantum.Apex,
                    Source = payment.Account,
                    Destination = payment.Destination,
                    Amount = payment.Amount,
                    Asset = payment.Asset,
                    TransactionHash = payment.TransactionHash
                };

                effectsContainer.AddWithdrawalCreate(withdrawal, Global.WithdrawalStorage);
                effectsContainer.AddVaultAccountSequenceUpdate(Global.VaultAccount, Global.VaultAccount.SequenceNumber + 1, Global.VaultAccount.SequenceNumber);
            }
            else
            {
                //if the current request is payment, then we can process it immediately
                effectsContainer.AddBalanceUpdate(destAccount.Account, payment.Asset, payment.Amount);

                effectsContainer.AddUnlockLiabilities(paymentAccount, payment.Asset, payment.Amount);
                effectsContainer.AddBalanceUpdate(paymentAccount, payment.Asset, -payment.Amount);
            }

            var effects = effectsContainer.GetEffects();

            var accountEffects = effects.Where(e => ByteArrayPrimitives.Equals(e.Pubkey, payment.Account)).ToList();
            return Task.FromResult(envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        public override Task Validate(MessageEnvelope envelope)
        {
            ValidateNonce(envelope);

            var payment = (envelope.Message as RequestQuantum).RequestEnvelope.Message as PaymentRequestBase;
            if (payment == null)
                throw new InvalidOperationException("The quantum must be an instance of PaymentRequestBase");

            if (payment.Account == null || payment.Account.IsZero())
                throw new InvalidOperationException("Source should be valid public key");

            if (payment.Destination == null || payment.Destination.IsZero())
                throw new InvalidOperationException("Destination should be valid public key");

            if (payment.Destination.Equals(payment.Account) && !(payment is WithdrawalRequest))
                throw new InvalidOperationException("Source and destination must be different public keys");

            if (payment.Amount <= 0)
                throw new InvalidOperationException("Amount should be greater than 0");

            if (!Global.AssetIds.Contains(payment.Asset))
                throw new InvalidOperationException($"Asset {payment.Asset} is not supported");

            var account = payment.AccountWrapper.Account;
            if (account == null)
                throw new Exception("Quantum source has no account");

            var balance = account.Balances.Find(b => b.Asset == payment.Asset);
            if (balance == null || !balance.HasSufficientBalance(payment.Amount))
                throw new InvalidOperationException("Insufficient funds");

            if (payment.MessageType == MessageTypes.WithdrawalRequest || Global.AccountStorage.GetAccount(payment.Destination) == null)
            {
                var tx = payment.GenerateTransaction();
                if (!Global.IsAlpha && !ByteArrayPrimitives.Equals(payment.TransactionHash, tx.Hash()))
                    throw new Exception("Transaction hashes are not equal.");
                payment.AssignTransactionXdr(tx);
            }

            return Task.CompletedTask;
        }
    }
}
