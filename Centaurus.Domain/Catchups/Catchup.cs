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
    internal class Catchup : ContextualBase, IDisposable
    {
        public Catchup(ExecutionContext context)
            : base(context)
        {
            InitTimer();
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private Dictionary<RawPubKey, CatchupQuantaBatch> allAuditorStates = new Dictionary<RawPubKey, CatchupQuantaBatch>();
        private Dictionary<RawPubKey, CatchupQuantaBatch> validAuditorStates = new Dictionary<RawPubKey, CatchupQuantaBatch>();
        private System.Timers.Timer applyDataTimer;

        public async Task AddAuditorState(RawPubKey pubKey, CatchupQuantaBatch auditorState)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                logger.Trace($"Auditor state from {pubKey.GetAccountId()} received by AlphaCatchup.");
                if (Context.NodesManager.CurrentNode.State != State.Rising)
                {
                    logger.Warn($"Auditor state messages can be only handled when Alpha is in rising state. State sent by {((KeyPair)pubKey).AccountId}");
                    return;
                }

                if (!(applyDataTimer.Enabled || pubKey.Equals(Context.Settings.KeyPair))) //start timer
                    applyDataTimer.Start();

                if (!allAuditorStates.TryGetValue(pubKey, out var pendingAuditorBatch))
                {
                    pendingAuditorBatch = auditorState;
                    allAuditorStates.Add(pubKey, auditorState);
                    if (applyDataTimer.Enabled)
                        applyDataTimer.Reset();
                    logger.Trace($"Auditor state from {pubKey.GetAccountId()} added.");
                }
                else if (!AddQuanta(pubKey, pendingAuditorBatch, auditorState)) //check if auditor sent all quanta already
                {
                    logger.Warn($"Unable to add auditor {pubKey.GetAccountId()} state.");
                    return;
                }

                if (pendingAuditorBatch.HasMore) //wait while auditor will send all quanta it has
                {
                    logger.Trace($"Auditor {pubKey.GetAccountId()} has more quanta. Timer is reset.");
                    applyDataTimer.Reset(); //if timer is running reset it. We need to try to wait all possible auditors data 
                    return;
                }

                logger.Trace($"Auditor {pubKey.GetAccountId()} state is validated.");

                validAuditorStates.Add(pubKey, pendingAuditorBatch);

                int majority = Context.GetMajorityCount(),
                totalAuditorsCount = Context.GetTotalAuditorsCount();
                var completedStatesCount = allAuditorStates.Count(s => !s.Value.HasMore);
                if (completedStatesCount == totalAuditorsCount)
                    await TryApplyAuditorsData();
            }
            catch (Exception exc)
            {
                Context.NodesManager.CurrentNode.Failed(new Exception("Error on adding auditors state", exc));
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private bool AddQuanta(RawPubKey pubKey, CatchupQuantaBatch currentBatch, CatchupQuantaBatch newBatch)
        {
            if (!currentBatch.HasMore
                || newBatch.HasMore && newBatch.Quanta.Count < 1) //prevent spamming
            {
                logger.Trace($"Unable to add auditor's {pubKey.GetAccountId()} quanta.");
                currentBatch.HasMore = false;
                return false;
            }
            currentBatch.HasMore = newBatch.HasMore;
            var lastAddedApex = ((Quantum)currentBatch.Quanta.LastOrDefault().Quantum).Apex;
            foreach (var quantumItem in newBatch.Quanta)
            {
                var currentQuantum = (Quantum)quantumItem.Quantum;
                if (currentQuantum.Apex != lastAddedApex + 1)
                    return false;
                lastAddedApex = currentQuantum.Apex;
                currentBatch.Quanta.Add(quantumItem);
            }
            logger.Trace($"Auditor's {pubKey.GetAccountId()} quanta added.");
            return true;
        }

        private async Task TryApplyAuditorsData()
        {
            try
            {
                logger.Trace($"Try apply auditors data.");
                if (Context.NodesManager.CurrentNode.State != State.Rising)
                    return;

                int majority = Context.GetMajorityCount(),
                totalAuditorsCount = Context.GetTotalAuditorsCount();

                if (Context.HasMajority(validAuditorStates.Count))
                {
                    await ApplyAuditorsData();
                    allAuditorStates.Clear();
                    validAuditorStates.Clear();
                    Context.NodesManager.CurrentNode.Rised();
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
                Context.NodesManager.CurrentNode.Failed(new Exception("Error during raising.", exc));
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
        }

        private async Task ApplyQuanta(List<(Quantum quantum, List<NodeSignatureInternal> signatures)> quanta)
        {
            foreach (var quantumItem in quanta)
            {
                Context.ResultManager.Add(new QuantumSignatures { Apex = quantumItem.quantum.Apex, Signatures = quantumItem.signatures });

                //compute quantum hash before processing
                var originalQuantumHash = quantumItem.quantum.ComputeHash();

                var processingItem = Context.QuantumHandler.HandleAsync(quantumItem.quantum, QuantumSignatureValidator.Validate(quantumItem.quantum));

                await processingItem.OnAcknowledged;

                //compute quantum hash after processing
                var processedQuantumHash = quantumItem.quantum.ComputeHash();
                //TODO: do we need some extra checks here?
                if (!ByteArrayPrimitives.Equals(originalQuantumHash, processedQuantumHash))
                    throw new Exception("Quantum hash are not equal on restore.");
            }
        }

        private List<(Quantum quantum, List<NodeSignatureInternal> signatures)> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = allAuditorStates.Values
                .SelectMany(a => a.Quanta)
                .GroupBy(q => ((Quantum)q.Quantum).Apex)
                .OrderBy(q => q.Key);

            var validQuanta = new List<(Quantum quantum, List<NodeSignatureInternal> signatures)>();

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

                if (quantum == null)
                    throw new Exception($"Unable to get quantum data for apex {currentQuantaGroup.Key}.");

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

        private (Quantum quantum, List<NodeSignatureInternal> signatures) GetQuantumData(ulong apex, List<CatchupQuantaBatchItem> allQuanta, int alphaId, List<RawPubKey> auditors)
        {
            var payloadHash = default(byte[]);
            var quantum = default(Quantum);
            var signatures = new Dictionary<int, NodeSignatureInternal>();

            foreach (var currentItem in allQuanta)
            {
                var currentQuantum = (Quantum)currentItem.Quantum;

                //compute current quantum payload hash
                var currentPayloadHash = currentQuantum.GetPayloadHash();

                //verify that quantum has alpha signature
                if (!currentItem.Signatures.Any(s => s.AuditorId == alphaId))
                    continue;

                //validate each signature
                foreach (var signature in currentItem.Signatures)
                {
                    //skip if current auditor is already presented in signatures, but force alpha signature to be checked
                    if (signature.AuditorId != alphaId && signatures.ContainsKey(signature.AuditorId))
                        continue;

                    //try get auditor
                    var signer = auditors.ElementAtOrDefault(signature.AuditorId);
                    //if auditor is not found or it's signature already added, move to the next signature
                    if (signer == null || !signature.PayloadSignature.IsValid(signer, currentPayloadHash))
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
                    if (currentItem.Quantum is WithdrawalRequestQuantum transactionQuantum)
                    {
                        var provider = Context.PaymentProvidersManager.GetManager(transactionQuantum.Provider);
                        if (!provider.IsTransactionValid(transactionQuantum.Transaction, transactionQuantum.WithdrawalRequest.ToProviderModel(), out var error))
                            throw new Exception($"Transaction is invalid.\nReason: {error}");

                        if (!provider.AreSignaturesValid(transactionQuantum.Transaction, new SignatureModel { Signer = signature.TxSigner, Signature = signature.TxSignature }))
                            //skip invalid signature
                            continue;
                    }
                    signatures[signature.AuditorId] = signature;
                }

                //continue if quantum already set
                if (quantum != null)
                    continue;

                quantum = currentQuantum;
                payloadHash = currentPayloadHash;
            }

            return (quantum, signatures.Values.ToList());
        }

        public void Dispose()
        {
            applyDataTimer?.Dispose();
            applyDataTimer = null;
        }
    }
}
