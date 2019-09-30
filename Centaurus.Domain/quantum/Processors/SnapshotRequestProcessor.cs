using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class SnapshotRequestProcessor : IQuantumRequestProcessor
    {
        public MessageTypes SupportedMessageType => MessageTypes.SnapshotQuantum;

        public ResultMessage Process(MessageEnvelope envelope)
        {
            return envelope.CreateResult(ResultStatusCodes.Success);
        }

        public void Validate(MessageEnvelope envelope)
        {
            var snapshotQuantum = (envelope.Message as SnapshotQuantum)
                ?? throw new ArgumentException($"Unexpected message type. Only messages of type {typeof(SnapshotQuantum).FullName} are supported.");
            var snapshot = Global.SnapshotManager.InitSnapshot();
            if (Global.Settings.IsAlpha)
                snapshotQuantum.Hash = snapshot.ComputeHash();
            else
            {
                if (!ByteArrayPrimitives.Equals(snapshot.ComputeHash(), snapshotQuantum.Hash))
                    throw new Exception("Snapshot hashes are not equal");

                var snapshotEnvelope = envelope.CreateResult(ResultStatusCodes.Success).CreateEnvelope();
                snapshotEnvelope.Sign(Global.Settings.KeyPair);
                //local snapshot should have signature, otherwise auditors could not validate it
                snapshot.Confirmation = snapshotEnvelope;

                Global.SnapshotManager.SavePendingSnapshot();
            }
        }
    }
}
