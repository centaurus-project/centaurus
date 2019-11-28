using Centaurus.DAL.Models;
using System.Collections.Generic;

namespace Centaurus.DAL
{
    public class DiffObject
    {
        public StellarInfo StellarInfoData { get; set; }
        public List<Account> Accounts { get; set; }
        public List<Balance> Balances { get; set; }
        public List<EffectModel> Effects { get; set; }
        public List<QuantumModel> Quanta { get; set; }
        public List<Order> Orders { get; set; }
        public List<Withdrawal> Widthrawals { get; set; }
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

            public ulong Nonce { get; set; }
        }

        public class Order : BaseDiffModel
        {
            public ulong OrderId { get; set; }

            public double Price { get; set; }

            public long Amount { get; set; }

            public byte[] Pubkey { get; set; }
        }
        public class StellarInfo : BaseDiffModel
        {
            public long Ledger { get; set; }
            public long VaultSequence { get; set; }
        }
        public class Withdrawal : BaseDiffModel
        {
            public ulong Apex { get; set; }

            public byte[] RawWithdrawal { get; set; }
            
            public byte[] TransactionHash { get; set; }
        }

        #endregion
    }
}
