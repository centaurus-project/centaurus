using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class Withdrawal
    {
        public MessageEnvelope Envelope { get; set; }

        public long Apex => ((Quantum)Envelope.Message).Apex;

        public byte[] Hash { get; set; }

        public AccountWrapper Source => ((RequestQuantum)Envelope.Message).RequestMessage.AccountWrapper;

        public List<WithdrawalItem> Withdrawals { get; set; }

        public List<DecoratedSignature> Signatures { get; set; }

        public long MaxTime { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentTime">Time in unix time seconds</param>
        /// <returns></returns>
        public bool IsExpired(long currentTime)
        {
            return currentTime - MaxTime > Global.MaxTxSubmitDelay;
        }

        public static Withdrawal GetWithdrawal(MessageEnvelope messageEnvelope, ConstellationSettings constellationSettings)
        {
            var withdrawalRequest = ((WithdrawalRequest)((RequestQuantum)messageEnvelope.Message).RequestMessage);
            var transaction = withdrawalRequest.DeserializeTransaction();
            var transactionHash = transaction.Hash();
            var withdrawal = new Withdrawal
            {
                Hash = transactionHash,
                Envelope = messageEnvelope
            };
            withdrawal.Withdrawals = transaction.GetWithdrawals(withdrawal.Source.Account, constellationSettings);
            return withdrawal;
        }
    }

    public class WithdrawalItem
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public RawPubKey Destination { get; set; }
    }


}