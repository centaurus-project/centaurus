using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public partial class UpdatesManager
    {
        class UpdatesContainer
        {
            public UpdatesContainer(uint id = 0)
            {
                Id = id;
            }

            public uint Id { get; }

            public bool IsCompleted { get; private set; }

            private object batchSyncRoot = new { };
            private List<IPersistentModel> batch = new List<IPersistentModel>();

            private HashSet<ulong> accounts = new HashSet<ulong>();

            public bool HasCursorUpdate { get; set; }

            public int QuantaCount { get; private set; }

            public int EffectsCount { get; private set; }

            public ulong FirstApex;
            public ulong LastApex;

            public void AddAffectedAccounts(IEnumerable<ulong> accounts)
            {
                this.accounts.UnionWith(accounts);
            }

            public void AddQuantum(QuantumPersistentModel quantum, int effectsCount)
            {
                lock (pendingQuantaSyncRoot)
                {
                    pendingQuanta.Add(quantum.Apex, quantum);
                    QuantaCount++;
                    EffectsCount += effectsCount;
                    if (FirstApex == 0)
                        FirstApex = quantum.Apex;
                    LastApex = quantum.Apex;
                }
            }

            public void AddConstellation(SettingsPersistentModel persistentModel)
            {
                lock (batchSyncRoot)
                    batch.Add(persistentModel);
            }

            public void AddQuantumRefs(IEnumerable<QuantumRefPersistentModel> quantumRefs)
            {
                lock (batchSyncRoot)
                    batch.AddRange(quantumRefs);
            }

            public void Complete(ExecutionContext context)
            {
                if (IsCompleted)
                    throw new InvalidOperationException("Already completed.");

                lock (batchSyncRoot)
                {
                    if (accounts.Count > 0)
                    {
                        var accountModels = accounts.Select(a => context.AccountStorage.GetAccount(a).ToPersistentModel()).Cast<IPersistentModel>().ToList();
                        batch.AddRange(accountModels);
                    }

                    if (HasCursorUpdate)
                        batch.Add(new CursorsPersistentModel { Cursors = context.PaymentProvidersManager.GetAll().ToDictionary(k => k.Id, v => v.Cursor) });

                    IsCompleted = true;
                }
            }

            private object pendingQuantaSyncRoot = new { };
            private Dictionary<ulong, QuantumPersistentModel> pendingQuanta { get; } = new Dictionary<ulong, QuantumPersistentModel>();

            public void AddSignatures(ulong apex, List<AuditorResult> signatures)
            {
                lock (pendingQuantaSyncRoot)
                {
                    if (!pendingQuanta.Remove(apex, out var quantum))
                        throw new InvalidOperationException($"Unable to find quantum with {apex} apex.");
                    if (signatures != null) //it could be null on force save
                        quantum.Signatures = signatures.Select(s => s.Signature.ToPersistenModel()).ToList();

                    lock (batchSyncRoot)
                        batch.Add(quantum);

                    logger.Trace($"Quantum {apex} signatures received. Batch {Id} awaits for {pendingQuanta.Count} signatures.");
                }
            }

            internal List<IPersistentModel> GetUpdates(bool force = true)
            {
                lock (batchSyncRoot)
                {
                    //force all quanta adding to batch
                    if (force)
                        lock (pendingQuantaSyncRoot)
                        {
                            var pendingApexes = pendingQuanta.Keys.ToList();
                            foreach (var apex in pendingApexes)
                                AddSignatures(apex, null);
                        }
                    return batch;
                }
            }

            public bool AreSignaturesCollected
            {
                get
                {
                    lock (pendingQuantaSyncRoot)
                        return pendingQuanta.Count == 0;
                }
            }
        }
    }
}
