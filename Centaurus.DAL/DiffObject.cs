using Centaurus.DAL.Models;
using Centaurus.Models;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Centaurus.DAL
{
    public class DiffObject
    {
        public List<QuantumModel> Quanta { get; } = new List<QuantumModel>();

        public SettingsModel ConstellationSettings { get; set; }

        public Dictionary<int, PaymentCursor> Cursors { get; } = new Dictionary<int, PaymentCursor>();

        public Dictionary<int, Account> Accounts { get; } = new Dictionary<int, Account>();

        public Dictionary<BsonObjectId, Balance> Balances { get; } = new Dictionary<BsonObjectId, Balance>();

        public Dictionary<ulong, Order> Orders { get; } = new Dictionary<ulong, Order>();

        public int EffectsCount { get; set; }

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

        public class Account : BaseDiffModel
        {
            public int Id { get; set; }

            public byte[] PubKey { get; set; }

            public long Nonce { get; set; }

            public long? Withdrawal { get; set; }

            public RequestRateLimitsModel RequestRateLimits { get; set; }
        }

        public class Balance : BaseDiffModel
        {
            public BsonObjectId Id { get; set; }

            public long AmountDiff { get; set; }

            public long LiabilitiesDiff { get; set; }
        }

        public class Order : BaseDiffModel
        {
            public ulong OrderId { get; set; }

            public double Price { get; set; }

            public long AmountDiff { get; set; }

            public long QuoteAmountDiff { get; set; }

            public int Account { get; set; }
        }

        public class PaymentCursor : BaseDiffModel
        {
            public int Provider { get; set; }

            public string Cursor { get; set; }
        }

        #endregion
    }
}