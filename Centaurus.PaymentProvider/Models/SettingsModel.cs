using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider.Models
{
    public class SettingsModel
    {
        public string Provider { get; set; }

        public string Name { get; set; }

        public string Vault { get; set; }

        public string InitCursor { get; set; }

        public List<AssetModel> Assets { get; set; }

        /// <summary>
        /// Submit delay in seconds
        /// </summary>
        public int PaymentSubmitDelay { get; set; }

        public string Id => PaymentProviderBase.GetProviderId(Provider, Name);
    }
}
