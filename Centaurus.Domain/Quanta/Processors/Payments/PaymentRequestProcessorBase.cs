using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{
    public abstract class PaymentRequestProcessorBase : ClientRequestProcessorBase
    {
        public override ResultMessage Process(MessageEnvelope envelope)
        {
            UpdateNonce(envelope);

            var payment = (envelope.Message as RequestQuantum).RequestEnvelope.Message as PaymentRequestBase;

            AccountData vaultAccount = Global.VaultAccount;

            //if withdrawal requested or if account doesn't exist in Centaurus, we need to build transaction
            if (payment.MessageType == MessageTypes.WithdrawalRequest || Global.AccountStorage.GetAccount(payment.Destination) == null)
            {
                Asset asset = new AssetTypeNative();
                if (payment.Asset != 0)
                    asset = Global.Constellation.Assets.Find(a => a.Id == payment.Asset).ToAsset();

                var transaction = TransactionHelper.BuildPaymentTransaction(
                    new TransactionBuilderOptions(vaultAccount, 10_000/*TODO: move fee to settings*/, payment.Memo),
                    new KeyPair(payment.Destination.ToArray()),
                    asset,
                    payment.Amount.ToString()
                );
                payment.TransactionXdr = transaction.ToRawEnvelopeXdr();
                payment.TransactionHash = transaction.Hash();

                Global.WithdrawalStorage.Add(payment);
            }

            var account = Global.AccountStorage.GetAccount(payment.Account);
            var balance = account.Balances.Find(b => b.Asset == payment.Asset);
            balance.LockLiabilities(payment.Amount);

            //TODO: add effects
            return envelope.CreateResult(ResultStatusCodes.Success);
        }

        public override void Validate(MessageEnvelope envelope)
        {
            ValidateNonce(envelope);

            var payment = (envelope.Message as RequestQuantum).RequestEnvelope.Message as PaymentRequestBase;
            if (payment == null)
                throw new InvalidOperationException("The quantum must be an instance of PaymentRequestBase");

            if (payment.Account == null || payment.Account.IsZero())
                throw new InvalidOperationException("Source should be valid public key");

            if (payment.Amount <= 0)
                throw new InvalidOperationException("Amount should be greater than 0");

            if (!Global.AssetIds.Contains(payment.Asset))
                throw new InvalidOperationException($"Asset {payment.Asset} is not supported");

            var account = Global.AccountStorage.GetAccount(payment.Account);
            if (account == null)
                throw new Exception("Quantum source has no account");

            var balance = account.Balances.Find(b => b.Asset == payment.Asset);
            if (balance == null || !balance.HasSufficientBalance(payment.Amount))
                throw new InvalidOperationException("Insufficient funds");
        }
    }
}
