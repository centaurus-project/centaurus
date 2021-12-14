using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class ExecutionContextExtensions
    {
        public static List<RawPubKey> GetAuditors(this ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return (context.ConstellationSettingsManager.Current == null
                    ? context.Settings.GenesisAuditors.Select(a => (RawPubKey)a.PubKey)
                    : context.ConstellationSettingsManager.Current.Auditors.Select(a => a.PubKey))
                    .ToList();
        }

        public static ConstellationInfo GetInfo(this ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var currentNode = context.NodesManager.CurrentNode;
            var info = new ConstellationInfo
            {
                State = currentNode.State,
                Apex = context.QuantumHandler.CurrentApex,
                PubKey = currentNode.AccountId
            };

            var constellationSettings = context.ConstellationSettingsManager.Current; 
            if (constellationSettings != null)
            {
                info.Providers = constellationSettings.Providers.ToArray();
                info.Auditors = constellationSettings.Auditors
                    .Select(a => new ConstellationInfo.Auditor { PubKey = a.PubKey.GetAccountId(), Address = a.Address })
                    .ToArray();
                info.Alpha = context.NodesManager.AlphaNode?.AccountId ?? "";
                info.MinAccountBalance = constellationSettings.MinAccountBalance;
                info.MinAllowedLotSize = constellationSettings.MinAllowedLotSize;
                info.Assets = constellationSettings.Assets.ToArray();
                info.RequestRateLimits = constellationSettings.RequestRateLimits;
            }
            return info;
        }

        public static async Task<ulong> HandleConstellationQuantum(this ExecutionContext context, ConstellationMessageEnvelope constellationInitEnvelope)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var constellationQuantum = new ConstellationQuantum { RequestEnvelope = constellationInitEnvelope };

            constellationQuantum.Validate(context);

            var quantumProcessingItem = context.QuantumHandler.HandleAsync(constellationQuantum, Task.FromResult(true));

            await quantumProcessingItem.OnProcessed;

            return quantumProcessingItem.Apex;
        }
    }
}