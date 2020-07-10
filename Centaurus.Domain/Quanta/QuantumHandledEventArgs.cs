using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantumHandledEventArg
    {
        public Quantum Quantum { get; set; }

        public ResultMessage Result { get; set; }

        public RawPubKey Account
        {
            get
            {
                var quantumRequest = Quantum as RequestQuantum;
                if (quantumRequest == null) throw new Exception("Quantum is not a request");
                return (quantumRequest.RequestEnvelope.Message as NonceRequestMessage).Account;
            }
        }
    }
}
