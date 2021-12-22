using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class ConstellationQuantumProcessorBase : QuantumProcessorBase
    {
        public ConstellationQuantumProcessorBase(ExecutionContext context)
            :base(context)
        {

        }

        public override Task Validate(QuantumProcessingItem processingItem)
        {
            ((ConstellationQuantum)processingItem.Quantum).Validate(Context);
            return Task.CompletedTask;
        }
    }
}
