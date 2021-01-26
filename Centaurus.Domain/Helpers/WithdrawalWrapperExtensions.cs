using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class WithdrawalWrapperExtensions
    {
        public static WithdrawalWrapper GetWithdrawal(MessageEnvelope messageEnvelope, ConstellationSettings constellationSettings)
        {
            var withdrawalRequest = ((WithdrawalRequest)((RequestQuantum)messageEnvelope.Message).RequestMessage);
            var transaction = withdrawalRequest.DeserializeTransaction();
            var transactionHash = transaction.Hash();
            var withdrawal = new WithdrawalWrapper
            {
                Hash = transactionHash,
                Envelope = messageEnvelope
            };
            withdrawal.Withdrawals = transaction.GetWithdrawals(withdrawal.Source.Account, constellationSettings);
            return withdrawal;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentTime">Time in unix time seconds</param>
        /// <returns></returns>
        public static bool IsExpired(this WithdrawalWrapper withdrawal, long currentTime)
        {
            return currentTime - withdrawal.MaxTime > Global.MaxTxSubmitDelay;
        }
    }
}
