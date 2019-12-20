﻿using Centaurus.Domain;
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

        private static AuditorStateMajorityCalc auditorStateMajorityCalc = new AuditorStateMajorityCalc();

        private static ApexMajorityCalc apexMajorityCalc = new ApexMajorityCalc();

        /// <summary>
        /// We need to discover smallest auditor apex to get snapshot for it
        /// </summary>
        public static async Task SetApex(RawPubKey pubKey, long apex)
        {
            if (Global.AppState.State != ApplicationState.Rising)
                throw new InvalidOperationException("Auditor state messages can be only handled when Alpha is in rising state");

            apexMajorityCalc.Add(pubKey, apex);
            if (apexMajorityCalc.MajorityResult == MajorityResults.Success)
            {
                //UNDONE: send majority apex
                //Notifier.NotifyAuditors(new AuditorStateRequest { TargetApex = apexMajorityCalc.MajorityData.First() });

                Notifier.NotifyAuditors(new AuditorStateRequest { TargetApex = await SnapshotManager.GetLastApex() });
            }
            else if (auditorStateMajorityCalc.MajorityResult == MajorityResults.Unreachable)
            {
                Global.AppState.State = ApplicationState.Failed;
                logger.Error("Majority of auditors are connected, but there is no consensus");
            }
        }

        public static async Task AddAuditorState(RawPubKey pubKey, AuditorState auditorState)
        {
            if (Global.AppState.State != ApplicationState.Rising)
                throw new InvalidOperationException("Auditor state messages can be only handled when Alpha is in rising state");

            auditorStateMajorityCalc.Add(pubKey, auditorState);

            if (auditorStateMajorityCalc.MajorityResult == MajorityResults.Success)
            {
                await ApplyAuditorsData(auditorStateMajorityCalc.MajorityData);
            }
            else if (auditorStateMajorityCalc.MajorityResult == MajorityResults.Unreachable)
            {
                Global.AppState.State = ApplicationState.Failed;
                logger.Error("Majority of auditors are connected, but there is no consensus");
            }
        }

        private static async Task ApplyAuditorsData(IEnumerable<AuditorState> largestGroup)
        {
            //if last snapshot is not null, we should aggregate all envelopes
            var snapshot = largestGroup.First().Snapshot;
            var validQuanta = new List<MessageEnvelope>();

            if (snapshot == null) //if it's null than Alpha was restarted right after initialization
            {
                //UNDONE: we need to init alpha with snapshot
                throw new NotImplementedException();
                snapshot = await SnapshotManager.GetSnapshot();
                if (snapshot.Apex != 0)
                    throw new Exception("Auditor snapshots cannot be null, if there is handled quanta");
            }
            else
                validQuanta = GetValidQuanta(snapshot.Apex, largestGroup);

            Global.Setup(snapshot, validQuanta);

            var alphaStateManager = (AlphaStateManager)Global.AppState;

            alphaStateManager.AlphaRised();

            Notifier.NotifyAuditors(await alphaStateManager.GetCurrentAlphaState());
        }

        private static List<MessageEnvelope> GetValidQuanta(long snapshotApex, IEnumerable<AuditorState> auditorStates)
        {
            //group all quanta by their apex
            var quanta = auditorStates
                .SelectMany(a => a.PendingQuantums)
                .Where(q => ((Quantum)q.Message).Apex > snapshotApex)
                .GroupBy(q => ((Quantum)q.Message).Apex)
                .OrderBy(q => q.Key);

            if (quanta.Count() == 0)
                return new List<MessageEnvelope>();

            var lastQuantumApex = snapshotApex;
            var validQuanta = new List<MessageEnvelope>();

            foreach (var currentQuantaGroup in quanta)
            {
                //select only quanta that has alpha signature
                var validatedQuanta = currentQuantaGroup
                    .Where(q => q.Signatures.Any(s => s.Signer == (RawPubKey)Global.Settings.KeyPair.PublicKey)
                        && q.AreSignaturesValid());

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

        private abstract class MajorityCalc<T>
        {
            private object syncRoot = new { };

            internal void Add(RawPubKey pubKey, T data)
            {
                lock (syncRoot)
                {
                    if (rawData.ContainsKey(pubKey))
                        return;
                    rawData.Add(pubKey, data);

                    int majority = MajorityHelper.GetMajorityCount(),
                    totalAuditorsCount = MajorityHelper.GetTotalAuditorsCount();

                    if (MajorityResult != MajorityResults.Unknown || rawData.Count < majority)
                        return;

                    var biggestConsensusCount = GetConsensusGroup().Count();
                    var possibleConsensusCount = (totalAuditorsCount - rawData.Count) + biggestConsensusCount;
                    if (biggestConsensusCount >= majority)
                    {
                        MajorityResult = MajorityResults.Success;
                    }
                    else if (possibleConsensusCount < majority)
                    {
                        MajorityResult = MajorityResults.Unreachable;
                        return;
                    }

                }

                //we have a consensus if we get here
#if !DEBUG
                    //Sleep for 10 seconds to make sure that all active auditors are connected
                    Thread.Sleep(10000);
#endif

                //reload data. New data could arrive during timeout
                MajorityData = GetConsensusGroup();
            }

            protected Dictionary<RawPubKey, T> rawData { get; } = new Dictionary<RawPubKey, T>();

            protected abstract IEnumerable<T> GetConsensusGroup();

            internal IEnumerable<T> MajorityData { get; private set; }

            internal MajorityResults MajorityResult { get; private set; }
        }

        private class AuditorStateMajorityCalc : MajorityCalc<AuditorState>
        {
            protected override IEnumerable<AuditorState> GetConsensusGroup()
            {
                //group auditor states by snapshot hash
                var groupedSnapshots = rawData.Values
                    //snapshot could be null if it's first auditor connection
                    .GroupBy(s => s.Snapshot?.ComputeHash() ?? new byte[] { }, new ByteArrayComparer());
                return groupedSnapshots.OrderByDescending(g => g.Count()).First();
            }
        }

        private class ApexMajorityCalc : MajorityCalc<long>
        {
            protected override IEnumerable<long> GetConsensusGroup()
            {
                //just return repeated smallest apex to make shared code work
                return Enumerable.Repeat(rawData.Values.Min(), rawData.Count);
            }
        }
    }
}
