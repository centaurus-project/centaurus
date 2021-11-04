using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public partial class UpdatesManager
    {
        public class UpdatesContainer : ContextualBase
        {
            public UpdatesContainer(ExecutionContext context, uint id = 0)
                : base(context)
            {
                Id = id;
            }

            public uint Id { get; }

            public bool IsCompleted { get; private set; }

            private List<IPersistentModel> batch = new List<IPersistentModel>();

            private HashSet<RawPubKey> accounts = new HashSet<RawPubKey>();

            public bool HasCursorUpdate { get; set; }

            public int QuantaCount { get; private set; }

            public int EffectsCount { get; private set; }

            public ulong FirstApex { get; private set; }

            public int AffectedAccountsCount => accounts.Count;

            /// <summary>
            /// Date when first quantum was added
            /// </summary>
            public DateTime InitDate { get; private set; }

            public ulong LastApex { get; private set; }

            public void AddAffectedAccounts(IEnumerable<RawPubKey> accounts)
            {
                this.accounts.UnionWith(accounts);
            }

            public void AddQuantum(QuantumPersistentModel quantum, int effectsCount)
            {
                pendingQuanta.Add(quantum);
                batch.Add(quantum);
                if (FirstApex == 0)
                {
                    FirstApex = quantum.Apex;
                    InitDate = DateTime.UtcNow;
                }
                QuantaCount++;
                EffectsCount += effectsCount;
                LastApex = quantum.Apex;
            }

            public void AddConstellation(SettingsPersistentModel persistentModel)
            {
                batch.Add(persistentModel);
            }

            public void AddQuantumRefs(IEnumerable<QuantumRefPersistentModel> quantumRefs)
            {
                batch.AddRange(quantumRefs);
            }

            public void Complete(ExecutionContext context)
            {
                if (IsCompleted)
                    throw new InvalidOperationException("Already completed.");

                if (accounts.Count > 0)
                {
                    var accountModels = accounts
                        .Select(a => context.AccountStorage.GetAccount(a).ToPersistentModel())
                        .Cast<IPersistentModel>()
                        .ToList();
                    batch.AddRange(accountModels);
                }

                if (HasCursorUpdate)
                    batch.Add(new CursorsPersistentModel { Cursors = context.PaymentProvidersManager.GetAll().ToDictionary(k => k.Id, v => v.Cursor) });

                IsCompleted = true;
            }

            private List<QuantumPersistentModel> pendingQuanta { get; } = new List<QuantumPersistentModel>();

            internal List<IPersistentModel> GetUpdates()
            {
                return batch;
            }

            internal bool GetPendingQuanta(out List<QuantumPersistentModel> quanta)
            {
                quanta = new List<QuantumPersistentModel>();
                foreach (var quantum in pendingQuanta)
                {
                    if (quantum.Signatures == null)
                    {
                        if (!Context.ResultManager.TryGetSignatures(quantum.Apex, out var signatures))
                        {
                            //quantum was handled with errors
                            logger.Error($"Quantum {quantum.Apex} doesn't have signatures.");
                            return false;
                        }
                        quantum.Signatures = signatures.Select(s => s.ToPersistenModel()).ToList();
                    }
                    quanta.Add(quantum);
                }
                return true;
            }

            public bool AreSignaturesCollected
            {
                get
                {
                    return pendingQuanta.Count == pendingQuanta.Count(q => q.Signatures != null);
                }
            }
        }
    }
}
