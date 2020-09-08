//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Centaurus.Models;
//using CommandLine;
//using stellar_dotnet_sdk;

//namespace Centaurus.Domain
//{
//    public class Payment : ClientRequestProcessorBase<WithdrawalProcessorContext>
//    {
//        //public override Task<ResultMessage> Process(WithdrawalProcessorContext context)
//        //{
//        //    UpdateNonce(context.EffectProcessorsContainer);

//        //    var requestQuantum = (RequestQuantum)context.Envelope.Message;

//        //    var payment = (PaymentRequestBase)requestQuantum.RequestEnvelope.Message;

//        //    var paymentAccount = payment.AccountWrapper.Account;

//        //    AccountData vaultAccount = Global.VaultAccount;

//        //    context.EffectProcessorsContainer.AddLockLiabilities(paymentAccount, payment.Asset, payment.Amount);
//        //    var destAccount = Global.AccountStorage.GetAccount(payment.Destination);

//        //    //if withdrawal requested or if account doesn't exist in Centaurus, we need to build transaction
//        //    if (payment.MessageType == MessageTypes.WithdrawalRequest || destAccount == null)
//        //    {
//        //        var withdrawal = new Withdrawal
//        //        {
//        //            Apex = requestQuantum.Apex,
//        //            Source = payment.Account,
//        //            Destination = payment.Destination,
//        //            Amount = payment.Amount,
//        //            Asset = payment.Asset,
//        //            TransactionHash = payment.TransactionHash
//        //        };

//        //        effectsContainer.AddWithdrawalCreate(withdrawal, Global.WithdrawalStorage);
//        //        //effectsContainer.AddVaultAccountSequenceUpdate(Global.VaultAccount, Global.VaultAccount.SequenceNumber + 1, Global.VaultAccount.SequenceNumber);
//        //    }
//        //    else
//        //    {
//        //        //if the current request is payment, then we can process it immediately
//        //        effectsContainer.AddBalanceUpdate(destAccount.Account, payment.Asset, payment.Amount);

//        //        effectsContainer.AddUnlockLiabilities(paymentAccount, payment.Asset, payment.Amount);
//        //        effectsContainer.AddBalanceUpdate(paymentAccount, payment.Asset, -payment.Amount);
//        //    }

//        //    var effects = effectsContainer.GetEffects();

//        //    var accountEffects = effects.Where(e => ByteArrayPrimitives.Equals(e.Pubkey, payment.Account)).ToList();
//        //    return Task.FromResult(envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
//        //}

//        private List<Withdrawal> GetWithdrawals(Transaction transaction, Models.Account sourceAccount, long apex)
//        {
//            var payments = transaction.Operations
//                .Where(o => o is PaymentOperation)
//                .Cast<PaymentOperation>();
//            var transactionHash = transaction.Hash();
//            var withdrawals = new List<Withdrawal>();
//            foreach (var payment in payments)
//            {
//                if (!Global.Constellation.TryFindAssetSettings(payment.Asset, out var asset))
//                    throw new InvalidOperationException("Asset is not allowed by constellation.");

//                if (!ByteArrayPrimitives.Equals(payment.SourceAccount?.PublicKey, Global.VaultAccount))
//                    throw new InvalidOperationException("Only vault account can be used as payment source.");
//                var amount = Amount.ToXdr(payment.Amount);
//                if (amount < Global.MinWithdrawalAmount)
//                    throw new InvalidOperationException($"Min withdrawal amount is {Global.MinWithdrawalAmount} stroops.");
//                if (sourceAccount.GetBalance(asset.Id)?.HasSufficientBalance(amount) ?? false)
//                    throw new InvalidOperationException($"Insufficient balance.");

//                withdrawals.Add(new Withdrawal
//                {
//                    Asset = asset.Id,
//                    Amount = Amount.ToXdr(payment.Amount),
//                    Destination = payment.Destination.PublicKey,
//                    Source = sourceAccount.Pubkey,
//                    Apex = apex,
//                    TransactionHash = transactionHash
//                });
//            }
//            if (withdrawals.GroupBy(w => w.Asset).Any(g => g.Count() > 1))
//                throw new InvalidOperationException("Multiple payments for the same asset.");
//            return withdrawals;
//        }

//        private void ValidateChangeTrustOperations(Transaction transaction, IEnumerable<Withdrawal> withdrawals)
//        {
//            var changeTrustOps = transaction.Operations
//                .Where(o => o is ChangeTrustOperation)
//                .Cast<ChangeTrustOperation>()
//                .ToArray();

//            foreach (var changeTrustOp in changeTrustOps)
//            {
//                if (changeTrustOp.SourceAccount != null) //allow change trustline only for tx source 
//                    throw new InvalidOperationException("Change trustline operation only supported for transaction source account.");

//                if (changeTrustOp.Asset is AssetTypeNative)
//                    throw new InvalidOperationException("Change trustline operation only supported for non-native assets.");

//                var limit = Amount.ToXdr(changeTrustOp.Limit);
//                if (limit == 0)
//                    throw new InvalidOperationException("Trustline deletion is not allowed.");

//                if (!Global.Constellation.TryFindAssetSettings(changeTrustOp.Asset, out var asset))
//                    throw new InvalidOperationException("Asset is not allowed by constellation.");
//                var assetWithdrawal = withdrawals.FirstOrDefault(w => w.Asset == asset.Id);
//                if (assetWithdrawal == null)
//                    throw new InvalidOperationException("Change trustline operations allowed only for assets that are used in payments.");
//                if (assetWithdrawal.Amount > limit)
//                    throw new InvalidOperationException("Change trustline limit must be greater or equal to payment amount.");
//            }

//        }

//        private void ValidateTx(PaymentRequestBase payment)
//        {
//            var transaction = payment.DeserializeTransaction();
//            var txSourceAccount = transaction.SourceAccount;
//            if (ByteArrayPrimitives.Equals(Global.VaultAccount.KeyPair.PublicKey, txSourceAccount.PublicKey))
//                throw new InvalidOperationException("Vault account cannot be used as transaction source.");

//            if (transaction.TimeBounds.MaxTime <= 0)
//                throw new InvalidOperationException("Max time should be set.");

//            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
//            if ((transaction.TimeBounds.MaxTime + Global.TxSubmitThreshold) - currentTime > 1000)
//                throw new InvalidOperationException("Transaction max time is to far.");

//            var withdrawals = GetWithdrawals(transaction, payment.AccountWrapper.Account, 0);
//            if (withdrawals.Count() < 1)
//                throw new InvalidOperationException("No payment operations.");

//            ValidateChangeTrustOperations()
//        }

//        public override Task Validate(MessageEnvelope envelope)
//        {
//            ValidateNonce(envelope);

//            var payment = (envelope.Message as RequestQuantum).RequestEnvelope.Message as PaymentRequestBase;
//            if (payment == null)
//                throw new InvalidOperationException("The quantum must be an instance of PaymentRequestBase");

//            if (payment.Account == null || payment.Account.IsZero())
//                throw new InvalidOperationException("Source should be valid public key");

//            if (payment.Destination == null || payment.Destination.IsZero())
//                throw new InvalidOperationException("Destination should be valid public key");

//            if (payment.Destination.Equals(payment.Account) && !(payment is WithdrawalRequest))
//                throw new InvalidOperationException("Source and destination must be different public keys");

//            if (payment.Amount <= 0)
//                throw new InvalidOperationException("Amount should be greater than 0");

//            if (!Global.AssetIds.Contains(payment.Asset))
//                throw new InvalidOperationException($"Asset {payment.Asset} is not supported");

//            var account = payment.AccountWrapper.Account;
//            if (account == null)
//                throw new Exception("Quantum source has no account");

//            var balance = account.Balances.Find(b => b.Asset == payment.Asset);
//            if (balance == null || !balance.HasSufficientBalance(payment.Amount))
//                throw new InvalidOperationException("Insufficient funds");

//            if (payment.MessageType == MessageTypes.WithdrawalRequest || Global.AccountStorage.GetAccount(payment.Destination) == null)
//            {
//                var tx = payment.GenerateTransaction();
//                if (!ByteArrayPrimitives.Equals(payment.TransactionHash, tx.Hash()))
//                    throw new Exception("Transaction hashes are not equal.");
//                payment.AssignTransactionXdr(tx);
//            }

//            return Task.CompletedTask;
//        }
//    }
//}
