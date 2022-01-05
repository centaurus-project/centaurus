using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class ConstellationUpdateProcessorBase: ConstellationQuantumProcessorBase
    {
        public ConstellationUpdateProcessorBase(ExecutionContext context)
            :base(context)
        {
        }

        /// <summary>
        /// Registers new constellation settings and updates nodes
        /// </summary>
        /// <param name="processingItem"></param>
        /// <param name="newSettings"></param>
        /// <returns></returns>
        protected async Task UpdateConstellationSettings(QuantumProcessingItem processingItem, ConstellationSettings newSettings)
        {
            processingItem.AddConstellationUpdate(newSettings, Context.ConstellationSettingsManager.Current);

            Context.ConstellationSettingsManager.Update(newSettings);

            await Context.NodesManager.SetNodes(newSettings.Auditors);
        }
    }
}
