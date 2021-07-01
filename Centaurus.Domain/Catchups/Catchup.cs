using Centaurus.Models;
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
            :base(context)
        {
            InitTimer();
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private Dictionary<RawPubKey, AuditorState> allAuditorStates = new Dictionary<RawPubKey, AuditorState>();
        private Dictionary<RawPubKey, AuditorState> validAuditorStates = new Dictionary<RawPubKey, AuditorState>();
        private System.Timers.Timer applyDataTimer;

        public async Task AddAuditorState(RawPubKey pubKey, AuditorState auditorState)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                if (!applyDataTimer.Enabled) //start timer
                    applyDataTimer.Start();

                logger.Trace($"Auditor state from {((KeyPair)pubKey).AccountId} received by AlphaCatchup.");
                if (Context.AppState.State != ApplicationState.Rising)
                {
                    logger.Warn($"Auditor state messages can be only handled when Alpha is in rising state. State sent by {((KeyPair)pubKey).AccountId}");
                    return;
                }

                if (!allAuditorStates.TryGetValue(pubKey, out var pendingAuditorState))
                {
                    pendingAuditorState = new AuditorState
                    {
                        HasMorePendingQuanta = true,
                        PendingQuanta = new List<MessageEnvelope>()
                    };
                    allAuditorStates.Add(pubKey, pendingAuditorState);
                    logger.Trace($"Auditor state from {((KeyPair)pubKey).AccountId} added.");
                }

                if (AddQuanta(pubKey, pendingAuditorState, auditorState)) //check if auditor sent all quanta already
                {
                    if (pendingAuditorState.HasMorePendingQuanta) //wait while auditor will send all quanta it has
                    {
                        logger.Trace($"Auditor {((KeyPair)pubKey).AccountId} has more quanta. Timer reseted.");
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
                logger.Error(exc, "Error on adding auditors state");
                Context.AppState.State = ApplicationState.Failed;
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

        private bool AddQuanta(RawPubKey pubKey, AuditorState currentState, AuditorState newAuditorState)
        {
            if (!currentState.HasMorePendingQuanta
                || newAuditorState.HasMorePendingQuanta && newAuditorState.PendingQuanta.Count < 1) //prevent spamming
            {
                logger.Trace($"Unable to add auditor's {((KeyPair)pubKey).AccountId} quanta.");
                currentState.HasMorePendingQuanta = false;
                return false;
            }
            currentState.HasMorePendingQuanta = newAuditorState.HasMorePendingQuanta;
            var lastAddedApex = currentState.PendingQuanta.LastOrDefault()?.Message.MessageId ?? -1;
            var alphaPubkey = (RawPubKey)Context.Settings.KeyPair.PublicKey;
            foreach (var envelope in newAuditorState.PendingQuanta)
            {
                var currentQuantum = (Quantum)envelope.Message;
                if (lastAddedApex != -1 && currentQuantum.Apex != lastAddedApex + 1)
                    return false;
                lastAddedApex = currentQuantum.Apex;
                if (envelope.Signatures.All(s => !s.Signer.Equals(alphaPubkey)) || !envelope.AreSignaturesValid())
                    return false;
                currentState.PendingQuanta.Add(envelope);
            }
            logger.Trace($"Auditor's {((KeyPair)pubKey).AccountId} quanta added.");
            return true;
        }

        private async Task TryApplyAuditorsData()
        {
            try
            {
                logger.Trace($"Try apply auditors data.");
                if (Context.AppState.State != ApplicationState.Rising)
                    return;

                int majority = Context.GetMajorityCount(),
                totalAuditorsCount = Context.GetTotalAuditorsCount();
                if (Context.HasMajority(validAuditorStates.Count, false))
                {
                    await ApplyAuditorsData();
                    allAuditorStates.Clear();
                    validAuditorStates.Clear();
                    logger.Trace($"Alpha is rised.");
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
                logger.Error(exc, "Error during raising.");
                Context.AppState.State = ApplicationState.Failed;
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
            var validQuanta = await GetValidQuanta();

            await ApplyQuanta(validQuanta);

            var alphaStateManager = Context.AppState;

            alphaStateManager.AlphaRised();

            Context.NotifyAuditors(Context.GetCurrentState().CreateEnvelope());
        }

        private async Task ApplyQuanta(List<MessageEnvelope> quanta)
        {
            var quantaCount = quanta.Count;
            for (var i = 0; i < quantaCount; i++)
            {
                var currentQuantumEnvelope = quanta[i];
                var currentQuantum = ((Quantum)currentQuantumEnvelope.Message);

                //try to unwrap for Alpha
                if (currentQuantum is RequestQuantum)
                    currentQuantumEnvelope = ((RequestQuantum)currentQuantum).RequestEnvelope;

                var resultMessage = await Context.QuantumHandler.HandleAsync(currentQuantumEnvelope, currentQuantum.Timestamp);
                var processedQuantum = (Quantum)resultMessage.OriginalMessage.Message;
                //TODO: check if we need some extra checks here
                if (!ByteArrayPrimitives.Equals(currentQuantum.ComputeHash(), processedQuantum.ComputeHash()))
                    throw new Exception("Apexes are not equal for a quantum on restore.");
            }
        }

        private async Task<List<MessageEnvelope>> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = validAuditorStates.Values
                .SelectMany(a => a.PendingQuanta)
                .GroupBy(q => ((Quantum)q.Message).Apex)
                .OrderBy(q => q.Key);

            if (quanta.Count() == 0)
                return new List<MessageEnvelope>();

            var lastQuantumApex = await Context.PersistenceManager.GetLastApex();
            var validQuanta = new List<MessageEnvelope>();

            foreach (var currentQuantaGroup in quanta)
            {
                //check if all quanta are the same
                if (currentQuantaGroup.GroupBy(q => q.ComputeHash(), ByteArrayComparer.Default).Count() > 1)
                    throw new Exception("Alpha's private key is compromised");

                if (lastQuantumApex + 1 != currentQuantaGroup.Key)
                    throw new Exception("A quantum is missing");

                validQuanta.Add(currentQuantaGroup.First());

                lastQuantumApex++;
            }

            return validQuanta;
        }

        public void Dispose()
        {
            applyDataTimer?.Dispose();
            applyDataTimer = null;
        }
    }
}
