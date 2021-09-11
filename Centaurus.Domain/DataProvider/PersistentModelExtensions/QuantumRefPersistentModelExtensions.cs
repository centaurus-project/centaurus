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
        public static QuantumRefPersistentModel ToPersistentModel(this AccountEffect effect, bool isQuantumInitiator)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            return new QuantumRefPersistentModel { Account = effect.Account, Apex = effect.Apex, IsQuantumInitiator = isQuantumInitiator };
        }
    }
}
