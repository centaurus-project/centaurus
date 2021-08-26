using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class RawPubKeyExtensions
    {
        public static string GetAccountId(this RawPubKey pubKey)
        {
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));

            return StrKey.EncodeStellarAccountId(pubKey);
        }
    }
}
