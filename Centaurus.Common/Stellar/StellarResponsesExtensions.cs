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
                ExistingTrustLines = accountResponse
                    .Balances
                    .Where(b => b.AssetIssuer != null)
                    .Select(b => AssetsHelper.GetCode(b.AssetCode, b.AssetIssuer))
                    .ToList()
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

        public static TxSubmitModel ToModel(this SubmitTransactionResponse accountResponse)
        {
            if (accountResponse == null)
                return null;
            return new TxSubmitModel
            {
                Hash = accountResponse.Hash,
                IsSuccess = accountResponse.IsSuccess(),
                ResultXdr = accountResponse.ResultXdr
            };
        }
    }
}
