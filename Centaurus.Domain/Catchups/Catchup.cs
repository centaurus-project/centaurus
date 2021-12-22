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

        public async Task AddNodeBatch(RawPubKey pubKey, CatchupQuantaBatch nodeBatch)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                logger.Trace($"Catchup quanta batch from {pubKey.GetAccountId()} received by Catchup.");
                if (Context.NodesManager.CurrentNode.State != State.Rising)
                {
                    logger.Warn($"Catchup quanta batch messages can be only handled when Alpha is in rising state. State sent by {((KeyPair)pubKey).AccountId}");
                    return;
                }

                if (!(applyDataTimer.Enabled)) //start timer
                    applyDataTimer.Start();

                if (!TryGetNodeBatch(pubKey, nodeBatch, out var aggregatedNodeBatch))
                    return;

                if (aggregatedNodeBatch.HasMore) //wait while auditor will send all quanta it has
                {
                    logger.Trace($"Auditor {pubKey.GetAccountId()} has more quanta. Timer is reset.");
                    applyDataTimer.Reset(); //if timer is running reset it. We need to try to wait all possible auditors data 
                    return;
                }

                logger.Trace($"Auditor {pubKey.GetAccountId()} state is validated.");

                validAuditorStates.Add(pubKey, aggregatedNodeBatch);

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

        private bool TryGetNodeBatch(RawPubKey pubKey, CatchupQuantaBatch batch, out CatchupQuantaBatch aggregatedNodeBatch)
        {
            if (!allAuditorStates.TryGetValue(pubKey, out aggregatedNodeBatch))
            {
                aggregatedNodeBatch = batch;
                allAuditorStates.Add(pubKey, batch);
                applyDataTimer.Reset();
                logger.Trace($"Auditor state from {pubKey.GetAccountId()} added.");
            }
            else if (!AddQuanta(pubKey, aggregatedNodeBatch, batch)) //check if auditor sent all quanta already
            {
                logger.Warn($"Unable to add auditor {pubKey.GetAccountId()} state.");
                return false;
            }

            return true;
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
                    var validQuanta = GetValidQuanta();
                    await ApplyQuanta(validQuanta);
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
            applyDataTimer.Interval = TimeSpan.FromSeconds(Context.Settings.CatchupTimeout).TotalMilliseconds;
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

        private async Task ApplyQuanta(List<ValidatedQuantumData> quanta)
        {
            foreach (var quantumItem in quanta)
            {
                Context.ResultManager.Add(new MajoritySignaturesBatchItem { Apex = quantumItem.Quantum.Apex, Signatures = quantumItem.Signatures });

                //compute quantum hash before processing
                var originalQuantumHash = quantumItem.Quantum.ComputeHash();

                var processingItem = Context.QuantumHandler.HandleAsync(quantumItem.Quantum, QuantumSignatureValidator.Validate(quantumItem.Quantum));

                await processingItem.OnAcknowledged;

                //compute quantum hash after processing
                var processedQuantumHash = quantumItem.Quantum.ComputeHash();
                //TODO: do we need some extra checks here?
                if (!ByteArrayPrimitives.Equals(originalQuantumHash, processedQuantumHash))
                    throw new Exception("Quantum hash are not equal on restore.");
            }
        }

        private List<ValidatedQuantumData> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = allAuditorStates.Values
                .SelectMany(a => a.Quanta)
                .GroupBy(q => ((Quantum)q.Quantum).Apex)
                .OrderBy(q => q.Key);

            var validQuanta = new List<ValidatedQuantumData>();

            if (quanta.Count() == 0)
                return validQuanta;

            //get last known apex
            var lastQuantumApex = Context.DataProvider.GetLastApex();
            //get current auditors settings
            var auditorsSettings = GetAuditorsSettings(Context.ConstellationSettingsManager.Current);

            foreach (var currentQuantaGroup in quanta)
            {
                if (lastQuantumApex + 1 != currentQuantaGroup.Key)
                    throw new Exception("A quantum is missing");

                if (!TryGetQuantumData(currentQuantaGroup.Key, currentQuantaGroup.ToList(), auditorsSettings, out var validatedQuantumData)) //if we unable to get quanta with majority, than stop processing newer quanta
                    break;

                validQuanta.Add(validatedQuantumData);

                lastQuantumApex++;

                //try to update constellation info
                if (validatedQuantumData.Quantum is ConstellationQuantum constellationQuantum
                    && constellationQuantum.RequestMessage is ConstellationUpdate constellationUpdate)
                {
                    auditorsSettings = GetAuditorsSettings(constellationUpdate.ToConstellationSettings(currentQuantaGroup.Key));
                }
            }

            return validQuanta;
        }

        private List<RawPubKey> GetAuditorsSettings(ConstellationSettings constellation)
        {
            if (Context.NodesManager.AlphaNode == null)
                throw new ArgumentNullException("Alpha node is not connected.");

            return constellation.Auditors.Select(n => n.PubKey).ToList();
        }

        private bool TryGetQuantumData(ulong apex, List<CatchupQuantaBatchItem> allApexQuanta, List<RawPubKey> auditors, out ValidatedQuantumData validatedQuantumData)
        {
            validatedQuantumData = null;
            //compute and group quanta by payload hash
            var grouped = allApexQuanta
                .GroupBy(q => ((Quantum)q.Quantum).GetPayloadHash(), ByteArrayComparer.Default);

            //validate each quantum data group
            var validatedQuantaData = GetValidatedQuantaData(auditors, grouped);

            //get majority for current auditors set
            var majorityCount = MajorityHelper.GetMajorityCount(auditors.Count);
            //get all quanta data with majority
            var quantaDataWithMajority = validatedQuantaData.Where(a => a.Signatures.Count >= majorityCount).ToList();
            if (quantaDataWithMajority.Count > 1)
            {
                //throw exception. The case must be investigated
                throw new Exception($"Conflict found for apex {apex}. {quantaDataWithMajority.Count} quanta data sets with majority.");
            }

            //if we have quanta data with majority, return it
            validatedQuantumData = quantaDataWithMajority.FirstOrDefault();
            return validatedQuantumData != null;
        }

        private List<ValidatedQuantumData> GetValidatedQuantaData(List<RawPubKey> auditors, IEnumerable<IGrouping<byte[], CatchupQuantaBatchItem>> grouped)
        {
            var validatedQuantaData = new List<ValidatedQuantumData>();
            foreach (var quantaGroup in grouped)
                validatedQuantaData.Add(ProcessQuantumGroup(auditors, quantaGroup));
            return validatedQuantaData;
        }

        private ValidatedQuantumData ProcessQuantumGroup(List<RawPubKey> auditors, IGrouping<byte[], CatchupQuantaBatchItem> quantaGroup)
        {
            var quantumHash = quantaGroup.Key;
            var quantum = (Quantum)quantaGroup.First().Quantum;
            var allSignatures = quantaGroup.SelectMany(q => q.Signatures);
            var validSignatures = new Dictionary<int, NodeSignatureInternal>();

            //validate each signature
            foreach (var signature in allSignatures)
            {
                //skip if current auditor is already presented in signatures
                if (validSignatures.ContainsKey(signature.NodeId))
                    continue;

                //try to get the node public key
                var signer = auditors.ElementAtOrDefault(signature.NodeId - 1); //node id is index + 1

                //if the node is not found or it's signature is invalid, move to the next signature
                if (signer == null || !signature.PayloadSignature.IsValid(signer, quantumHash))
                    continue;

                //check transaction and it's signatures
                if (quantum is WithdrawalRequestQuantum transactionQuantum)
                {
                    var provider = Context.PaymentProvidersManager.GetManager(transactionQuantum.Provider);
                    if (!provider.IsTransactionValid(transactionQuantum.Transaction, transactionQuantum.WithdrawalRequest.ToProviderModel(), out var error))
                        throw new Exception($"Transaction is invalid.\nReason: {error}");

                    if (!provider.AreSignaturesValid(transactionQuantum.Transaction, new SignatureModel { Signer = signature.TxSigner, Signature = signature.TxSignature }))
                        //skip invalid signature
                        continue;
                }
                //add valid signatures
                validSignatures.Add(signature.NodeId, signature);
            }
            return new ValidatedQuantumData(quantum, validSignatures.Values.ToList());
        }

        public void Dispose()
        {
            applyDataTimer?.Dispose();
            applyDataTimer = null;
        }

        class ValidatedQuantumData
        {
            public ValidatedQuantumData(Quantum quantum, List<NodeSignatureInternal> signatures)
            {
                Quantum = quantum;
                Signatures = signatures;
            }

            public Quantum Quantum { get; }

            public List<NodeSignatureInternal> Signatures { get; }
        }
    }
}
