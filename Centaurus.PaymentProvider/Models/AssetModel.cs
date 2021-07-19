using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider.Models
{
    public class AssetModel
    {
        public string Token { get; set; }

        public string CentaurusAsset { get; set; }

        public bool IsVirtual { get; set; }
    }
}
