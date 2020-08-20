﻿using Centaurus.Models;
using stellar_dotnet_sdk.requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    //TODO: write tests for this processors
    public class VaultSequenceResetQuantumProcessor : IQuantumRequestProcessor
    {
        public MessageTypes SupportedMessageType => MessageTypes.VaultSequenceResetQuantum;

        public Task<ResultMessage> Process(MessageEnvelope envelope, EffectProcessorsContainer effectProcessorsContainer)
        {
            var vaultSequenceReset = (VaultSequenceResetQuantum)envelope.Message;
            effectProcessorsContainer.AddVaultAccountSequenceUpdate(Global.VaultAccount, vaultSequenceReset.VaultSequence, Global.VaultAccount.SequenceNumber);

            var withdrawals = Global.WithdrawalStorage.GetAll().ToList();
            foreach (var withdrawal in withdrawals)
            {
                var withdrawalSourceAccount = Global.AccountStorage.GetAccount(withdrawal.Source);
                effectProcessorsContainer.AddWithdrawalRemove(withdrawal, Global.WithdrawalStorage);
                effectProcessorsContainer.AddUnlockLiabilities(withdrawalSourceAccount.Account, withdrawal.Asset, withdrawal.Amount);
            }
            return Task.FromResult(envelope.CreateResult(ResultStatusCodes.Success, effectProcessorsContainer.GetEffects().ToList()));
        }

        public async Task Validate(MessageEnvelope envelope)
        {
            if (!Global.IsAlpha) //no need to validate sequence, because it's generated by Alpha itself
            {
                var vaultSequenceReset = (VaultSequenceResetQuantum)envelope.Message;
                var currentVaultSequence = await StellarAccountHelper.GetVaultStellarAccount();
                if (vaultSequenceReset.VaultSequence != currentVaultSequence.SequenceNumber)
                    throw new InvalidOperationException("Specified sequence is not equal to fetched sequence.");

                var withdrawals = Global.WithdrawalStorage.GetAll().ToList();
                foreach (var withdrawal in withdrawals)
                {
                    var tx = await Global.StellarNetwork.Server.GetTransaction(withdrawal.TransactionHash);
                    if (tx == null && withdrawal.Apex <= vaultSequenceReset.LastSubmittedWithdrawalApex)
                        throw new InvalidOperationException("Withdrawal's apex is lower than last known submitted apex.");
                    else if (tx != null && withdrawal.Apex > vaultSequenceReset.LastSubmittedWithdrawalApex)
                        throw new InvalidOperationException("Withdrawal's apex is higher than last known submitted apex.");
                }
            }
        }
    }
}