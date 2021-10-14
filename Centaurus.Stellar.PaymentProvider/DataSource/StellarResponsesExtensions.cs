using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Stellar
{
    public static class StellarResponsesExtensions
    {
        public static TxModel ToModel(this TransactionResponse transactionResponse)
        {
            if (transactionResponse == null)
                return null;

            return new TxModel
            {
                PagingToken = long.Parse(transactionResponse.PagingToken),
                EnvelopeXdr = transactionResponse.EnvelopeXdr,
                Hash = transactionResponse.Hash,
                IsSuccess = transactionResponse.Successful
            };
        }

        public static AccountModel ToModel(this AccountResponse accountResponse)
        {
            if (accountResponse == null)
                return null;

            return new AccountModel
            {
                KeyPair = accountResponse.KeyPair,
                SequenceNumber = accountResponse.SequenceNumber,
                Signers = accountResponse.Signers
                    .Select(s => new AccountModel.Signer { PubKey = KeyPair.FromAccountId(s.Key).PublicKey, Weight = s.Weight })
                    .ToList(),
                ExistingTrustLines = accountResponse
                    .Balances
                    .Where(b => b.AssetIssuer != null)
                    .Select(b => AssetsHelper.GetCode(b.AssetCode, b.AssetIssuer))
                    .ToList(),
                Thresholds = new AccountModel.ThresholdsSettings { High = accountResponse.Thresholds.HighThreshold, Low = accountResponse.Thresholds.LowThreshold, Medium = accountResponse.Thresholds.MedThreshold }
            };
        }

        public static ITransactionBuilderAccount ToITransactionBuilderAccount(this AccountModel accountModel)
        {
            if (accountModel == null)
                return null;
            return new TxBuilderAccountModel
            {
                KeyPair = accountModel.KeyPair,
                SequenceNumber = accountModel.SequenceNumber
            };
        }

        public static TxSubmitModel ToModel(this SubmitTransactionResponse submitResponse)
        {
            if (submitResponse == null)
                return null;
            return new TxSubmitModel
            {
                Hash = submitResponse.Hash,
                IsSuccess = submitResponse.IsSuccess(),
                ResultXdr = submitResponse.ResultXdr,
                FeeCharged = submitResponse.Result.FeeCharged
            };
        }
    }
}
