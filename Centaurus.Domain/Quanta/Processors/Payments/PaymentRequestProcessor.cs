using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class PaymentRequestProcessor : WithdrawalProcessor
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.PaymentRequest;

        //public override Task<ResultMessage> Process(WithdrawalProcessorContext context)
        //{
        //    UpdateNonce(context.EffectProcessorsContainer);

        //    var requestQuantum = (RequestQuantum)context.Envelope.Message;

        //    var payment = (PaymentRequestBase)requestQuantum.RequestEnvelope.Message;

        //    var paymentAccount = payment.AccountWrapper.Account;

        //    AccountData vaultAccount = Global.VaultAccount;

        //    context.EffectProcessorsContainer.AddLockLiabilities(paymentAccount, payment.Asset, payment.Amount);
        //    var destAccount = Global.AccountStorage.GetAccount(payment.Destination);

        //    //if withdrawal requested or if account doesn't exist in Centaurus, we need to build transaction
        //    if (payment.MessageType == MessageTypes.WithdrawalRequest || destAccount == null)
        //    {
        //        var withdrawal = new Withdrawal
        //        {
        //            Apex = requestQuantum.Apex,
        //            Source = payment.Account,
        //            Destination = payment.Destination,
        //            Amount = payment.Amount,
        //            Asset = payment.Asset,
        //            TransactionHash = payment.TransactionHash
        //        };

        //        effectsContainer.AddWithdrawalCreate(withdrawal, Global.WithdrawalStorage);
        //        //effectsContainer.AddVaultAccountSequenceUpdate(Global.VaultAccount, Global.VaultAccount.SequenceNumber + 1, Global.VaultAccount.SequenceNumber);
        //    }
        //    else
        //    {
        //        //if the current request is payment, then we can process it immediately
        //        effectsContainer.AddBalanceUpdate(destAccount.Account, payment.Asset, payment.Amount);

        //        effectsContainer.AddUnlockLiabilities(paymentAccount, payment.Asset, payment.Amount);
        //        effectsContainer.AddBalanceUpdate(paymentAccount, payment.Asset, -payment.Amount);
        //    }

        //    var effects = effectsContainer.GetEffects();

        //    var accountEffects = effects.Where(e => ByteArrayPrimitives.Equals(e.Pubkey, payment.Account)).ToList();
        //    return Task.FromResult(envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        //}
    }
}
