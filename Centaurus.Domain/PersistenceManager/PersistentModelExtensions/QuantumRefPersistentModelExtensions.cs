using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public static class QuantumRefPersistentModelExtensions
    {
        public static QuantumRefPersistentModel ToPersistentModel(this Effect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            return new QuantumRefPersistentModel { AccountId = effect.Account, Apex = effect.Apex };
        }
    }
}
