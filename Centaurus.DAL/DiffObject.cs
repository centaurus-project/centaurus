using Centaurus.DAL.Models;
using System.Collections.Generic;

namespace Centaurus.DAL
{
    public class DiffObject
    {
        public ConstellationState StellarInfoData { get; set; }
        public List<Account> Accounts { get; set; }
        public List<Balance> Balances { get; set; }
        public List<EffectModel> Effects { get; set; }
        public List<QuantumModel> Quanta { get; set; }
        public List<Order> Orders { get; set; }
        public SettingsModel Settings { get; set; }
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
            public int AssetId { get; set; }

            public byte[] PubKey { get; set; }

            public long Amount { get; set; }

            public long Liabilities { get; set; }
        }

        public class Account : BaseDiffModel
        {
            public byte[] PubKey { get; set; }

            public long Nonce { get; set; }

            public RequestRateLimitsModel RequestRateLimits { get; set; }
        }

        public class Order : BaseDiffModel
        {
            public ulong OrderId { get; set; }

            public double Price { get; set; }

            public long Amount { get; set; }

            public byte[] Pubkey { get; set; }
        }
        public class ConstellationState : BaseDiffModel
        {
            public long Ledger { get; set; }
        }

        #endregion
    }
}
