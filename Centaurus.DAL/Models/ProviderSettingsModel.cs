using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class ProviderSettingsModel
    {
        [BsonId]
        public string ProviderId { get; set; }

        public string Provider { get; set; }

        public string Vault { get; set; }

        public string Name { get; set; }

        public string Cursor { get; set; }

        public int PaymentSubmitDelay { get; set; }

        public List<ProviderAssetModel> Assets { get; set; }
    }

    public class ProviderAssetModel
    {
        public bool IsVirtual { get; set; }

        public string Token { get; set; }

        public int CentaurusAsset { get; set; }
    }
}
