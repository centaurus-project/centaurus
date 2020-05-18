using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

                if (allAuditorStates.ContainsKey(pubKey))
                    return;
                allAuditorStates.Add(pubKey, auditorState);
                if (IsStateValid(auditorState))
                    validAuditorStates.Add(pubKey, auditorState);

                int majority = MajorityHelper.GetMajorityCount(),
                totalAuditorsCount = MajorityHelper.GetTotalAuditorsCount();

                if (allAuditorStates.Count < majority)
                    return;

                var possibleConsensusCount = (totalAuditorsCount - allAuditorStates.Count) + validAuditorStates.Count;
                if (validAuditorStates.Count >= majority)
                {
                    await ApplyAuditorsData();
                }
                else if (possibleConsensusCount < majority)
                {
                    Global.AppState.State = ApplicationState.Failed;
                    logger.Error("Majority of auditors are connected, but there is no consensus");
                }
            }
            catch (Exception exc)
            {
                Global.AppState.State = ApplicationState.Failed;
                logger.Error(exc, "Error on adding auditors state");
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Checks that all quanta has valid Alpha signature
        /// </summary>
        private static bool IsStateValid(AuditorState state)
        {
            return state.PendingQuantums.All(q => q.Signatures.Any(s => s.Signer.Equals((RawPubKey)Global.Settings.KeyPair.PublicKey))
                        && q.AreSignaturesValid());
        }

        private static async Task ApplyAuditorsData()
        {
            var validQuanta = await GetValidQuanta();

            await ApplyQuanta(validQuanta);

            var alphaStateManager = (AlphaStateManager)Global.AppState;

            alphaStateManager.AlphaRised();

            Notifier.NotifyAuditors(alphaStateManager.GetCurrentAlphaState().CreateEnvelope());
        }

        private static async Task ApplyQuanta(List<MessageEnvelope> quanta)
        { 
            var quantaCount = quanta.Count;
            for (var i = 0; i < quantaCount; i++)
            {
                var currentQuantumEnvelope = quanta[i];
                var currentQuantum = ((Quantum)currentQuantumEnvelope.Message);
                var quantumApex = currentQuantum.Apex;
                await Global.QuantumHandler.HandleAsync(currentQuantumEnvelope);
                if (quantumApex != currentQuantum.Apex)
                    throw new Exception("Apexes are not equal for a quantum on restore.");
            }
        }

        private static async Task<List<MessageEnvelope>> GetValidQuanta()
        {
            //group all quanta by their apex
            var quanta = validAuditorStates.Values
                .SelectMany(a => a.PendingQuantums)
                .GroupBy(q => ((Quantum)q.Message).Apex)
                .OrderBy(q => q.Key);

            if (quanta.Count() == 0)
                return new List<MessageEnvelope>();

            var lastQuantumApex = await SnapshotManager.GetLastApex();
            var validQuanta = new List<MessageEnvelope>();

            foreach (var currentQuantaGroup in quanta)
            {
                //check if all quanta are the same
                if (currentQuantaGroup.GroupBy(q => q.ComputeHash()).Count() > 1)
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
