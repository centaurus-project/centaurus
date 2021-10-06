using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    /// <summary>
    /// This class manages auditor snapshots and quanta when Alpha is rising
    /// </summary>
    public class Catchup : ContextualBase, IDisposable
    {
        public Catchup(ExecutionContext context)
            : base(context)
        {
            InitTimer();
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private Dictionary<RawPubKey, PendingQuantaBatch> allAuditorStates = new Dictionary<RawPubKey, PendingQuantaBatch>();
        private Dictionary<RawPubKey, PendingQuantaBatch> validAuditorStates = new Dictionary<RawPubKey, PendingQuantaBatch>();
        private System.Timers.Timer applyDataTimer;

        public async Task AddAuditorState(RawPubKey pubKey, QuantaBatch auditorState)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                logger.Trace($"Auditor state from {pubKey.GetAccountId()} received by AlphaCatchup.");
                if (Context.StateManager.State != State.Rising)
                {
                    logger.Warn($"Auditor state messages can be only handled when Alpha is in rising state. State sent by {((KeyPair)pubKey).AccountId}");
                    return;
                }

                if (!applyDataTimer.Enabled) //start timer
                    applyDataTimer.Start();

                if (!allAuditorStates.TryGetValue(pubKey, out var pendingAuditorBatch))
                {
                    pendingAuditorBatch = new PendingQuantaBatch
                    {
                        HasMorePendingQuanta = true
                    };
                    allAuditorStates.Add(pubKey, pendingAuditorBatch);
                    logger.Trace($"Auditor state from {pubKey.GetAccountId()} added.");
                }

                if (PendingQuantaBatch.TryCreate(auditorState, out var quantaBatch) && AddQuanta(pubKey, pendingAuditorBatch, quantaBatch)) //check if auditor sent all quanta already
                {
                    if (pendingAuditorBatch.HasMorePendingQuanta) //wait while auditor will send all quanta it has
                    {
                        logger.Trace($"Auditor {pubKey.GetAccountId()} has more quanta. Timer is reset.");
                        applyDataTimer.Reset(); //if timer is running reset it. We need to try to wait all possible auditors data 
                        return;
                    }
                    logger.Trace($"Auditor {pubKey.GetAccountId()} state is validated.");

                    validAuditorStates.Add(pubKey, pendingAuditorBatch);
                }

                int majority = Context.GetMajorityCount(),
                totalAuditorsCount = Context.GetTotalAuditorsCount();
                var completedStatesCount = allAuditorStates.Count(s => !s.Value.HasMorePendingQuanta) + 1; //+1 is current server
                if (completedStatesCount == totalAuditorsCount)
                    await TryApplyAuditorsData();
            }
            catch (Exception exc)
            {
                Context.StateManager.Failed(new Exception("Error on adding auditors state", exc));
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public void RemoveState(RawPubKey pubKey)
        {
            semaphoreSlim.Wait();
            try
            {
                allAuditorStates.Remove(pubKey);
                validAuditorStates.Remove(pubKey);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private bool AddQuanta(RawPubKey pubKey, PendingQuantaBatch currentState, PendingQuantaBatch newAuditorState)
        {
            if (!currentState.HasMorePendingQuanta
                || newAuditorState.HasMorePendingQuanta && newAuditorState.Quanta.Count < 1) //prevent spamming
            {
                logger.Trace($"Unable to add auditor's {pubKey.GetAccountId()} quanta.");
                currentState.HasMorePendingQuanta = false;
                return false;
            }
            currentState.HasMorePendingQuanta = newAuditorState.HasMorePendingQuanta;
            var lastAddedApex = (ulong)(currentState.Quanta.LastOrDefault()?.Quantum.Apex ?? 0);
            foreach (var processedQuantum in newAuditorState.Quanta)
            {
                var currentQuantum = processedQuantum.Quantum;
                if (lastAddedApex != 0 && currentQuantum.Apex != lastAddedApex + 1)
                    return false;
                lastAddedApex = currentQuantum.Apex;
                currentState.Quanta.Add(processedQuantum);
            }
            logger.Trace($"Auditor's {pubKey.GetAccountId()} quanta added.");
            return true;
        }

        private async Task TryApplyAuditorsData()
        {
            try
            {
                logger.Trace($"Try apply auditors data.");
                if (Context.StateManager.State != State.Rising)
                    return;

                int majority = Context.GetMajorityCount(),
                totalAuditorsCount = Context.GetTotalAuditorsCount();

                if (Context.HasMajority(validAuditorStates.Count, false))
                {
                    await ApplyAuditorsData();
                    allAuditorStates.Clear();
                    validAuditorStates.Clear();
                    logger.Trace($"Alpha is risen.");
                }
                else
                {
                    var connectedAccounts = allAuditorStates.Keys.Select(a => a.GetAccountId());
                    var validatedAccounts = validAuditorStates.Keys.Select(a => a.GetAccountId());
                    throw new Exception($"Unable to raise. Connected auditors: {string.Join(',', connectedAccounts)}; validated auditors: {string.Join(',', validatedAccounts)}; majority is {majority}.");
                }
            }
            catch (Exception exc)
            {
                Context.StateManager.Failed(new Exception("Error during raising.", exc));
            }
            finally
            {
                applyDataTimer.Stop();
            }
        }

        private void InitTimer()
        {
            applyDataTimer = new System.Timers.Timer();
            applyDataTimer.Interval = 15000; //15 sec
            applyDataTimer.AutoReset = false;
            applyDataTimer.Elapsed += ApplyDataTimer_Elapsed;
        }

        private void ApplyDataTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            semaphoreSlim.Wait();
            try
            {
                TryApplyAuditorsData().Wait();
            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        private async Task ApplyAuditorsData()
        {
            var validQuanta = GetValidQuanta();

            await ApplyQuanta(validQuanta);

            var alphaStateManager = Context.StateManager;

            alphaStateManager.Rised();
        }

        private async Task ApplyQuanta(List<(Quantum quantum, List<AuditorResult> signatures)> quanta)
        {
            var quantaCount = quanta.Count;
            foreach (var quantumItem in quanta)
            {
                foreach (var signature in quantumItem.signatures)
                {
                    Context.ResultManager.Add(signature);
                }

                //compute quantum hash before processing
                var originalQuantumHash = quantumItem.quantum.ComputeHash();

                var processingItem = Context.QuantumHandler.HandleAsync(quantumItem.quantum, QuantumSignatureValidator.Validate(quantumItem.quantum));

                await processingItem.OnAcknowledge;

                //compute quantum hash after processing
                var processedQuantumHash = quantumItem.quantum.ComputeHash();
                //TODO: do we need some extra checks here?
                if (!ByteArrayPrimitives.Equals(originalQuantumHash, processedQuantumHash))
                    throw new Exception("Quantum hash are not equal on restore.");
            }
        }

        private List<(Quantum quantum, List<AuditorResult> signatures)> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = allAuditorStates.Values
                .SelectMany(a => a.Quanta)
                .GroupBy(q => q.Quantum.Apex)
                .OrderBy(q => q.Key);

            var validQuanta = new List<(Quantum quantum, List<AuditorResult> signatures)>();

            if (quanta.Count() == 0)
                return validQuanta;

            //get last known apex
            var lastQuantumApex = Context.DataProvider.GetLastApex();
            //get current auditors settings
            var auditorsSettings = GetAuditorsSettings(Context.Constellation);

            foreach (var currentQuantaGroup in quanta)
            {
                if (lastQuantumApex + 1 != currentQuantaGroup.Key)
                    throw new Exception("A quantum is missing");

                var (quantum, signatures) = GetQuantumData(currentQuantaGroup.Key, currentQuantaGroup.ToList(), auditorsSettings.alphaId, auditorsSettings.auditors);

                if (!signatures.Any(s => s.Signature.AuditorId == auditorsSettings.alphaId))
                    throw new Exception("Quantum must contain at least Alpha signature");

                validQuanta.Add((quantum, signatures));

                lastQuantumApex++;

                //try to update constellation info
                if (quantum is ConstellationQuantum constellationQuantum
                    && constellationQuantum.RequestMessage is ConstellationUpdate constellationUpdate)
                {
                    auditorsSettings = GetAuditorsSettings(constellationUpdate.ToConstellationSettings(currentQuantaGroup.Key));
                }
            }

            return validQuanta;
        }

        private (int alphaId, List<RawPubKey> auditors) GetAuditorsSettings(ConstellationSettings constellation)
        {
            var auditors = constellation.Auditors
             .Select(a => new RawPubKey(a.PubKey))
             .ToList();
            var alphaId = constellation.GetAuditorId(constellation.Alpha);
            if (alphaId < 0)
                throw new ArgumentNullException("Unable to find Alpha id.");

            return (alphaId, auditors);
        }

        private (Quantum quantum, List<AuditorResult> signatures) GetQuantumData(ulong apex, List<PendingQuantum> allQuanta, int alphaId, List<RawPubKey> auditors)
        {
            var payloadHash = default(byte[]);
            var quantum = default(Quantum);
            var signatures = new List<AuditorResult>();
            foreach (var currentQuantum in allQuanta)
            {
                //compute current quantum payload hash
                var currentPayloadHash = currentQuantum.Quantum.GetPayloadHash();

                //validate each signature
                foreach (var signature in currentQuantum.Signatures)
                {
                    if (signatures.Any(s => s.Signature.AuditorId == signature.AuditorId))
                        continue;

                    //try get auditor
                    var signer = auditors.ElementAtOrDefault(signature.AuditorId);

                    //if auditor is not found or it's signature already added, move to the next signature
                    if (signer == null || !signature.PayloadSignature.IsValid(signer, payloadHash))
                        continue;

                    if (payloadHash != null && !ByteArrayPrimitives.Equals(payloadHash, currentPayloadHash))
                    {
                        //if there are several quanta with same apex but with different hash signed by Alpha
                        if (alphaId == signature.AuditorId)
                            throw new Exception($"Alpha {signer.GetAccountId()} private key is compromised. Apex {apex}.");
                        //skip invalid signature
                        continue;
                    }

                    //check transaction and it's signatures
                    if (currentQuantum.Quantum is WithdrawalRequestQuantum transactionQuantum)
                    {
                        var provider = Context.PaymentProvidersManager.GetManager(transactionQuantum.Provider);
                        if (!provider.IsTransactionValid(transactionQuantum.Transaction, transactionQuantum.WithdrawalRequest.ToProviderModel(), out var error))
                            throw new Exception($"Transaction is invalid.\nReason: {error}");

                        if (!provider.AreSignaturesValid(transactionQuantum.Transaction, new SignatureModel { Signer = signature.TxSigner, Signature = signature.TxSignature }))
                            //skip invalid signature
                            continue;
                    }
                    signatures.Add(new AuditorResult { Apex = quantum.Apex, Signature = signature });
                }

                //continue if quantum already set
                if (quantum != null)
                    continue;

                quantum = currentQuantum.Quantum;
                payloadHash = currentPayloadHash;
            }

            return (quantum, signatures);
        }

        public void Dispose()
        {
            applyDataTimer?.Dispose();
            applyDataTimer = null;
        }

        class PendingQuantaBatch
        {
            public List<PendingQuantum> Quanta { get; } = new List<PendingQuantum>();

            public bool HasMorePendingQuanta { get; set; }

            public static bool TryCreate(QuantaBatch quantaBatch, out PendingQuantaBatch pendingQuantaBatch)
            {
                pendingQuantaBatch = null;
                if (quantaBatch == null || quantaBatch.Quanta.Count != quantaBatch.Signatures.Count)
                    return false;

                pendingQuantaBatch = new PendingQuantaBatch();
                for (var i = 0; i < quantaBatch.Quanta.Count; i++)
                {
                    var quantum = (Quantum)quantaBatch.Quanta[i];
                    var signatures = quantaBatch.Signatures[i];
                    if (quantum.Apex != signatures.Apex)
                        return false;
                    pendingQuantaBatch.Quanta.Add(new PendingQuantum { Quantum = quantum, Signatures = signatures.Signatures });
                }

                var lastQuantum = pendingQuantaBatch.Quanta.LastOrDefault();

                pendingQuantaBatch.HasMorePendingQuanta = lastQuantum != null && lastQuantum.Quantum.Apex != quantaBatch.LastKnownApex;
                return true;
            }
        }
    }
}
