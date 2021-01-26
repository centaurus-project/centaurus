using Centaurus.Models;
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
    public static class AlphaCatchup
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private static Dictionary<RawPubKey, AuditorState> allAuditorStates = new Dictionary<RawPubKey, AuditorState>();
        private static Dictionary<RawPubKey, AuditorState> validAuditorStates = new Dictionary<RawPubKey, AuditorState>();

        public static async Task AddAuditorState(RawPubKey pubKey, AuditorState auditorState)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                if (Global.AppState.State != ApplicationState.Rising)
                    throw new InvalidOperationException("Auditor state messages can be only handled when Alpha is in rising state");

                if (allAuditorStates.TryGetValue(pubKey, out var pendingAuditorState))
                {
                    if (!pendingAuditorState.HasMorePendingQuanta) //check if auditor send all quanta already
                        return;
                    allAuditorStates[pubKey].PendingQuanta.AddRange(auditorState.PendingQuanta);
                    allAuditorStates[pubKey].HasMorePendingQuanta = auditorState.HasMorePendingQuanta;
                }
                else
                    allAuditorStates.Add(pubKey, auditorState);
                var currentAuditorState = allAuditorStates[pubKey];

                if (currentAuditorState.HasMorePendingQuanta) //wait while auditor will send all quanta it has
                    return;

                if (IsStateValid(currentAuditorState))
                    validAuditorStates.Add(pubKey, currentAuditorState);

                int majority = MajorityHelper.GetMajorityCount(),
                totalAuditorsCount = MajorityHelper.GetTotalAuditorsCount();

                var completedStatesCount = allAuditorStates.Count(s => !s.Value.HasMorePendingQuanta);

                if (completedStatesCount < majority)
                    return;

                var possibleConsensusCount = (totalAuditorsCount - completedStatesCount) + validAuditorStates.Count;
                if (validAuditorStates.Count >= majority)
                {
                    await ApplyAuditorsData();
                }
                else if (possibleConsensusCount < majority)
                {
                    logger.Error("Majority of auditors are connected, but there is no consensus");
                    Global.AppState.State = ApplicationState.Failed;
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on adding auditors state");
                Global.AppState.State = ApplicationState.Failed;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Checks that all quanta have valid Alpha signature
        /// </summary>
        private static bool IsStateValid(AuditorState state)
        {
            var alphaPubkey = (RawPubKey)Global.Settings.KeyPair.PublicKey;
            var lastApex = Global.QuantumStorage.CurrentApex;
            for (int i = 0; i < state.PendingQuanta.Count; i++)
            {
                var currentQuantumEnvelope = state.PendingQuanta[i];
                var currentQuantum = (Quantum)currentQuantumEnvelope.Message;
                if (!(lastApex + 1 == currentQuantum.Apex 
                    && currentQuantumEnvelope.Signatures.Any(s => s.Signer.Equals(alphaPubkey))
                    && currentQuantumEnvelope.AreSignaturesValid()))
                    return false;
                lastApex = currentQuantum.Apex;
            }
            return true;
        }

        private static async Task ApplyAuditorsData()
        {
            var validQuanta = await GetValidQuanta();

            await ApplyQuanta(validQuanta);

            var alphaStateManager = (AlphaStateManager)Global.AppState;

            alphaStateManager.AlphaRised();

            Notifier.NotifyAuditors(AlphaStateHelper.GetCurrentState().CreateEnvelope());
        }

        private static async Task ApplyQuanta(List<MessageEnvelope> quanta)
        {
            var quantaCount = quanta.Count;
            for (var i = 0; i < quantaCount; i++)
            {
                var currentQuantumEnvelope = quanta[i];
                var currentQuantum = ((Quantum)currentQuantumEnvelope.Message);

                //try to unwrap for Alpha
                if (currentQuantum is RequestQuantum)
                    currentQuantumEnvelope = ((RequestQuantum)currentQuantum).RequestEnvelope;

                var resultMessage = await Global.QuantumHandler.HandleAsync(currentQuantumEnvelope, currentQuantum.Timestamp);
                var processedQuantum = (Quantum)resultMessage.OriginalMessage.Message;
                //TODO: check if we need some extra checks here
                if (!ByteArrayPrimitives.Equals(currentQuantum.ComputeHash(), processedQuantum.ComputeHash()))
                    throw new Exception("Apexes are not equal for a quantum on restore.");
            }
        }

        private static async Task<List<MessageEnvelope>> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = validAuditorStates.Values
                .SelectMany(a => a.PendingQuanta)
                .GroupBy(q => ((Quantum)q.Message).Apex)
                .OrderBy(q => q.Key);

            if (quanta.Count() == 0)
                return new List<MessageEnvelope>();

            var lastQuantumApex = await Global.PersistenceManager.GetLastApex();
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
    }
}
