using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class AuditorCatchup
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task<ResultStatusCodes> Catchup(Snapshot alphaSnapshot)
        {
            try
            {
                if (!IsSnapshotValid(alphaSnapshot))
                    return ResultStatusCodes.SnapshotValidationFailed;

                //we should persist first snapshot immediately
                await SnapshotManager.InitFromSnapshot(alphaSnapshot);

                Global.Setup(alphaSnapshot);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                return ResultStatusCodes.InternalError;
            }

            return ResultStatusCodes.Success;
        }

        private static bool IsSnapshotValid(Snapshot snapshot)
        {
            try
            {
                var knownAudiotors = GetKnownAuditors();

                if (snapshot.Apex == 1)
                {
                    ValidateInitSnapshot(snapshot, knownAudiotors);
                    return true;
                }

                ValidateSnapshotSignatures(snapshot, knownAudiotors);

                return true;
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                return false;
            }
        }

        private static void ValidateSnapshotSignatures(Snapshot snapshot, List<RawPubKey> knownAudiotors)
        {
            var majorityCount = MajorityHelper.GetMajorityCount(knownAudiotors.Count);
            var validSignatures = 0;

            var signatures = snapshot.Signatures;

            //set Signatures to null, because snapshot hash computes without it
            snapshot.Signatures = null;

            var snapshotHash = snapshot.ComputeHash();
            if (signatures != null)
            {
                foreach (var signature in signatures)
                {
                    if (!knownAudiotors.Contains(signature.Signer))
                        continue;

                    if (signature.IsValid(snapshotHash))
                        validSignatures++;
                    if (validSignatures >= majorityCount)
                        break;
                }
            }

            if (validSignatures < majorityCount)
                throw new Exception("Snapshot has no majority");
        }

        private static void ValidateInitSnapshot(Snapshot snapshot, List<RawPubKey> knownAudiotors)
        {
            //it's init snapshot and it doesn't have signatures, just check that auditors are equal to default
            if (!Array.TrueForAll(snapshot.Settings.Auditors.ToArray(), a => knownAudiotors.Contains(a)))
                throw new Exception("Init snapshot auditors are not equal to default");
        }

        private static List<RawPubKey> GetKnownAuditors()
        {
            if (Global.Constellation != null)
                return Global.Constellation.Auditors;

            var settings = (AuditorSettings)Global.Settings;
            //we should have default auditors to make sure that Alpha provides snapshot valid snapshot
            var knownAuditors = settings.GenesisQuorum
                .Select(a => (RawPubKey)KeyPair.FromAccountId(a))
                .ToList();

            if (knownAuditors == null || knownAuditors.Count < 1)
                throw new Exception("Default auditors should be specified");

            return knownAuditors;
        }
    }
}