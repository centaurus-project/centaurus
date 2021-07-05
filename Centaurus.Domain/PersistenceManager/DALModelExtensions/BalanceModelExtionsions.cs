using Centaurus.DAL.Models;
using Centaurus.DAL.Mongo;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class BalanceModelExtionsions
    {
        public static Balance ToBalance(this BalanceModel balance)
        {
            var decodedAssetId = BalanceModelIdConverter.DecodeId(balance.Id);
            return new Balance
            {
                Asset = decodedAssetId.asset,
                Amount = balance.Amount
            };
        }
    }
}
