using Centaurus.DAL.Models;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Diagnostics;

namespace Centaurus.DAL
{
    public class DiffObject
    {
        public List<QuantumModel> Quanta { get; set; } = new List<QuantumModel>();

        public List<EffectModel> Effects { get; set; } = new List<EffectModel>();

        public SettingsModel ConstellationSettings { get; set; }

        public ConstellationState StellarInfoData { get; set; }

        public Dictionary<int, Account> Accounts { get; } = new Dictionary<int, Account>();

        public Dictionary<BsonObjectId, Balance> Balances { get; } = new Dictionary<BsonObjectId, Balance>();

        public Dictionary<ulong, Order> Orders { get; } = new Dictionary<ulong, Order>();

        public List<AssetModel> Assets { get; set; }

        #region DiffModels

        public abstract class BaseDiffModel
        {
            private bool isInserted;
            public bool IsInserted
            {
                get
                {
                    return isInserted;
                }
                set
                {
                    if (IsDeleted && value)
                        IsDeleted = false;
                    isInserted = value;
                }
            }

            private bool isDeleted;
            public bool IsDeleted
            {
                get
                {
                    return isDeleted;
                }
                set
                {
                    if (IsInserted && value)
                        IsInserted = false;
                    isDeleted = value;
                }
            }
        }

        public class Balance : BaseDiffModel
        {
            public BsonObjectId Id { get; set; }

            public long Amount { get; set; }

            public long Liabilities { get; set; }

            public void Merge(Balance balance)
            {
                Amount += balance.Amount;
                Liabilities += balance.Liabilities;
                IsDeleted = balance.IsDeleted;
            }
        }

        public class Account : BaseDiffModel
        {
            public int Id { get; set; }

            public byte[] PubKey { get; set; }

            public long Nonce { get; set; }

            public RequestRateLimitsModel RequestRateLimits { get; set; }

            public void Merge(Account account)
            {
                if (account.Nonce > 0)
                    Nonce = account.Nonce;
                if (account.RequestRateLimits != null)
                    RequestRateLimits = account.RequestRateLimits;
                IsDeleted = account.IsDeleted;
            }
        }

        public class Order : BaseDiffModel
        {
            public ulong OrderId { get; set; }

            public double Price { get; set; }

            public long Amount { get; set; }

            public int Account { get; set; }

            public void Merge(Order order)
            {
                //on update order amount always has negative value
                Amount += order.Amount;
                IsDeleted = order.IsDeleted;
            }
        }
        public class ConstellationState : BaseDiffModel
        {
            public long TxCursor { get; set; }
        }

        #endregion
    }
}
