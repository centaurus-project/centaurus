    using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{
    public class AccountQuantumDTO
    {
        public QuantumPersistentModel Quantum { get; set; }

        public bool IsInitiator { get; set; }
    }
}
