using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using NLog;
using System;
using System.Collections.Generic;
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
        private Dictionary<RawPubKey, QuantaBatch> allAuditorStates = new Dictionary<RawPubKey, QuantaBatch>();
        private Dictionary<RawPubKey, QuantaBatch> validAuditorStates = new Dictionary<RawPubKey, QuantaBatch>();
        private System.Timers.Timer applyDataTimer;

        public async Task AddAuditorState(RawPubKey pubKey, QuantaBatch auditorState)
        {
            await semaphoreSlim.WaitAsync();
            try
            {

                logger.Trace($"Auditor state from {((KeyPair)pubKey).AccountId} received by AlphaCatchup.");
                if (Context.StateManager.State != State.Rising)
                {
                    logger.Warn($"Auditor state messages can be only handled when Alpha is in rising state. State sent by {((KeyPair)pubKey).AccountId}");
                    return;
                }

                if (!applyDataTimer.Enabled) //start timer
                    applyDataTimer.Start();

                if (!allAuditorStates.TryGetValue(pubKey, out var pendingAuditorState))
                {
                    pendingAuditorState = new QuantaBatch
                    {
                        Quanta = new List<PendingQuantum>(),
                        HasMorePendingQuanta = true
                    };
                    allAuditorStates.Add(pubKey, pendingAuditorState);
                    logger.Trace($"Auditor state from {((KeyPair)pubKey).AccountId} added.");
                }

                if (AddQuanta(pubKey, pendingAuditorState, auditorState)) //check if auditor sent all quanta already
                {
                    if (pendingAuditorState.HasMorePendingQuanta) //wait while auditor will send all quanta it has
                    {
                        logger.Trace($"Auditor {((KeyPair)pubKey).AccountId} has more quanta. Timer is reset.");
                        applyDataTimer.Reset(); //if timer is running reset it. We need to try to wait all possible auditors data 
                        return;
                    }
                    logger.Trace($"Auditor {((KeyPair)pubKey).AccountId} state is validated.");

                    validAuditorStates.Add(pubKey, pendingAuditorState);
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

        private bool AddQuanta(RawPubKey pubKey, QuantaBatch currentState, QuantaBatch newAuditorState)
        {
            if (!currentState.HasMorePendingQuanta
                || newAuditorState.HasMorePendingQuanta && newAuditorState.Quanta.Count < 1) //prevent spamming
            {
                logger.Trace($"Unable to add auditor's {((KeyPair)pubKey).AccountId} quanta.");
                currentState.HasMorePendingQuanta = false;
                return false;
            }
            currentState.HasMorePendingQuanta = newAuditorState.HasMorePendingQuanta;
            var lastAddedApex = (ulong)(currentState.Quanta.LastOrDefault()?.MessageId ?? 0);
            foreach (var processedQuantum in newAuditorState.Quanta)
            {
                var currentQuantum = processedQuantum.Quantum;
                if (lastAddedApex != 0 && currentQuantum.Apex != lastAddedApex + 1)
                    return false;
                lastAddedApex = currentQuantum.Apex;
                currentState.Quanta.Add(processedQuantum);
            }
            logger.Trace($"Auditor's {((KeyPair)pubKey).AccountId} quanta added.");
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
                    var connectedAccounts = allAuditorStates.Keys.Select(a => ((KeyPair)a).AccountId);
                    var validatedAccounts = validAuditorStates.Keys.Select(a => ((KeyPair)a).AccountId);
                    throw new Exception($"Unable to raise. Connected auditors: {string.Join(',', connectedAccounts)}; validated auditors: {validatedAccounts}; majority is {majority}.");
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

        private async Task ApplyQuanta(List<(Quantum quantum, Dictionary<RawPubKey, AuditorResultMessage> signatures)> quanta)
        {
            var quantaCount = quanta.Count;
            foreach (var quantumItem in quanta)
            {
                foreach (var signature in quantumItem.signatures)
                {
                    Context.AuditResultManager.Add(signature.Value, signature.Key);
                }
                var resultMessage = await Context.QuantumHandler.HandleAsync(quantumItem.quantum);
                var processedQuantum = (Quantum)resultMessage.OriginalMessage.Message;
                //TODO: do we need some extra checks here?
                if (!ByteArrayPrimitives.Equals(quantumItem.quantum.ComputeHash(), processedQuantum.ComputeHash()))
                    throw new Exception("Apexes are not equal for a quantum on restore.");
            }
        }

        private List<(Quantum quantum, Dictionary<RawPubKey, AuditorResultMessage> signatures)> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = allAuditorStates.Values
                .SelectMany(a => a.Quanta)
                .GroupBy(q => q.Quantum.Apex)
                .OrderBy(q => q.Key);

            var validQuanta = new List<(Quantum quantum, Dictionary<RawPubKey, AuditorResultMessage> signatures)>();

            if (quanta.Count() == 0)
                return validQuanta;

            //get last known apex
            var lastQuantumApex = Context.PersistenceManager.GetLastApex();
            //get current constellation settings
            var constellation = Context.Constellation;
            var auditors = constellation.Auditors
                .Select(a => new RawPubKey(a.PubKey))
                .ToList();

            foreach (var currentQuantaGroup in quanta)
            {
                if (lastQuantumApex + 1 != currentQuantaGroup.Key)
                    throw new Exception("A quantum is missing");

                var (quantum, signatures) = GetQuantumData(currentQuantaGroup.Key, currentQuantaGroup.ToList(), constellation.Alpha, auditors);

                if (!signatures.ContainsKey(constellation.Alpha))
                    throw new Exception("Quantum must contain at least Alpha signature");

                validQuanta.Add((quantum, signatures));

                lastQuantumApex++;

                //try to update constellation info
                if (quantum is ConstellationQuantum constellationQuantum
                    && constellationQuantum.RequestMessage is ConstellationUpdate constellationUpdate)
                {
                    constellation = constellationUpdate.ToConstellationSettings(currentQuantaGroup.Key);
                    constellation.Auditors
                     .Select(a => new RawPubKey(a.PubKey))
                     .ToList();
                }
            }

            return validQuanta;
        }

        private (Quantum quantum, Dictionary<RawPubKey, AuditorResultMessage> signatures) GetQuantumData(ulong apex, List<PendingQuantum> allQuanta, RawPubKey alphaPubKey, List<RawPubKey> auditors)
        {
            var payloadHash = default(byte[]);
            var quantum = default(Quantum);
            var signatures = new Dictionary<RawPubKey, AuditorResultMessage>();
            foreach (var currentQuantum in allQuanta)
            {
                //compute current quantum payload hash
                var currentPayloadHash = currentQuantum.Quantum.GetPayloadHash();

                //validate each signature
                foreach (var signature in currentQuantum.Signatures)
                {
                    //try get auditor
                    var signer = default(RawPubKey);
                    foreach (var auditor in auditors)
                    {
                        if (signature.PayloadSignature.IsValid(auditor, payloadHash))
                        {
                            signer = auditor;
                            break;
                        }
                    }

                    //if auditor is not found or it's signature already added, move to the next signature
                    if (signer == null || signatures.ContainsKey(signer))
                        continue;

                    if (payloadHash != null && !ByteArrayPrimitives.Equals(payloadHash, currentPayloadHash))
                    {
                        //if there are several quanta with same apex but with different hash signed by Alpha
                        if (signer == alphaPubKey)
                            throw new Exception($"Alpha {alphaPubKey.GetAccountId()} private key is compromised. Apex {apex}.");
                        //skip invalid signature
                        continue;
                    }

                    //check transaction and it's signatures
                    if (currentQuantum.Quantum is WithdrawalRequestQuantum transactionQuantum)
                    {
                        var provider = Context.PaymentProvidersManager.GetManager(transactionQuantum.ProviderId);
                        if (!provider.IsTransactionValid(transactionQuantum.Transaction, transactionQuantum.WithdrawalRequest.ToProviderModel(), out var error))
                            throw new Exception($"Transaction is invalid.\nReason: {error}");

                        if (!provider.AreSignaturesValid(transactionQuantum.Transaction, new SignatureModel { Signer = signature.TxSigner, Signature = signature.TxSignature }))
                            //skip invalid signature
                            continue;
                    }
                    signatures.Add(signer, new AuditorResultMessage { Apex = quantum.Apex, Signature = signature });
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
    }
}
