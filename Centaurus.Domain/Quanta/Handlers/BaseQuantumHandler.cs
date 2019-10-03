using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseQuantumHandler
    {
        public abstract void Handle(MessageEnvelope envelope);

        public abstract void Start();
    }
}
