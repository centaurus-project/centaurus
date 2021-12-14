using Centaurus.Models;
using System;
using System.Linq;

namespace Centaurus.Domain.Quanta.Sync
{
    public partial class ApexItemsBatch<T>
    {
        class ApexItemsBatchPortion
        {
            public ApexItemsBatchPortion(ulong start, int size, ApexItemsBatch<T> source)
            {
                Start = lastSerializedBatchApex = start;
                Size = size;
                LastApex = start + (ulong)size;
                this.source = source ?? throw new ArgumentNullException(nameof(source));
            }

            protected ApexItemsBatch<T> source;

            private SyncPortion batch;
            private ulong lastSerializedBatchApex = 0;

            public SyncPortion GetBatch(bool force)
            {
                if (batch != null && lastSerializedBatchApex == LastApex)
                    return batch;

                if (force && source.LastApex >= lastSerializedBatchApex || source.LastApex >= LastApex)
                    if (lastSerializedBatchApex < source.LastApex)
                    {
                        batch = GetBatchData();
                        lastSerializedBatchApex = batch.LastDataApex;
                    }
                return batch;
            }

            protected SyncPortion GetBatchData()
            {
                switch (source)
                {
                    case ApexItemsBatch<MajoritySignaturesBatchItem> majoritySignaturesBatch:
                        {
                            var items = majoritySignaturesBatch.GetItems(Start, Size, true);
                            var batch = new MajoritySignaturesBatch
                            {
                                Items = items
                            };
                            return new SyncPortion(batch.CreateEnvelope<MessageEnvelopeSignless>().ToByteArray(), items.Last().Apex);
                        }
                    case ApexItemsBatch<SyncQuantaBatchItem> quantaBatch:
                        {
                            var items = quantaBatch.GetItems(Start, Size, true);
                            var batch = new SyncQuantaBatch
                            {
                                Quanta = items
                            };
                            return new SyncPortion(batch.CreateEnvelope<MessageEnvelopeSignless>().ToByteArray(), items.Last().Apex);
                        }
                    case ApexItemsBatch<SingleNodeSignaturesBatchItem> signaturesBatch:
                        {
                            var items = signaturesBatch.GetItems(Start, Size, true);
                            var batch = new SingleNodeSignaturesBatch
                            {
                                Items = items
                            };
                            return new SyncPortion(batch.CreateEnvelope<MessageEnvelopeSignless>().ToByteArray(), items.Last().Apex);
                        }
                    default:
                        throw new NotImplementedException($"{nameof(source)} is not supported.");
                }
            }

            public ulong Start { get; }

            public ulong LastApex { get; }

            public int Size { get; }
        }
    }
}
