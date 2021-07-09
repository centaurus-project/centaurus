using Centaurus.PaymentProvider;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ProviderCursorPersistentModelExtesnions
    {
        public static ProviderCursorPersistentModel ToPersistentModel(this PaymentProviderBase paymentProvider)
        {
            return new ProviderCursorPersistentModel { Provider = paymentProvider.Id, Cursor = paymentProvider.Cursor };
        }
    }
}
