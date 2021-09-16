using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public partial class UpdatesManager
    {
        public class UpdatesContainer
        {
            public UpdatesContainer(uint id = 0)
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
                pendingQuanta.Add(quantum.Apex, quantum);
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

            private Dictionary<ulong, QuantumPersistentModel> pendingQuanta { get; } = new Dictionary<ulong, QuantumPersistentModel>();

            public void AddSignatures(ulong apex, List<AuditorResult> signatures)
            {
                if (signatures == null)
                    throw new ArgumentNullException(nameof(signatures));
                if (!pendingQuanta.TryGetValue(apex, out var quantum))
                    throw new InvalidOperationException($"Unable to find quantum with {apex} apex.");
                quantum.Signatures = signatures.Select(s => s.Signature.ToPersistenModel()).ToList();
            }

            internal List<IPersistentModel> GetUpdates(bool force = true)
            {
                //force all quanta adding to batch
                //if (force)
                //    lock (pendingQuantaSyncRoot)
                //    {
                //        var pendingApexes = pendingQuanta.Keys.ToList();
                //        foreach (var apex in Context)
                //            AddSignatures(apex, null);
                //    }
                return batch;
            }

            public bool AreSignaturesCollected
            {
                get
                {
                    return pendingQuanta.Count == pendingQuanta.Count(q => q.Value.Signatures != null);
                }
            }
        }
    }
}
