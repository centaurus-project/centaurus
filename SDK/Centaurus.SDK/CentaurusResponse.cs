using Centaurus.Models;
using Centaurus.SDK.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.SDK
{
    public class CentaurusResponse
    {
        public CentaurusResponse(ConstellationInfo constellationInfo, int requestTimeout)
        {
            AlphaPubkey = new RawPubKey(constellationInfo.Vault);
            Auditors = constellationInfo.Auditors.Select(a => new RawPubKey(a)).ToArray();
            _ = StartRequestTimer(requestTimeout);
        }


        async Task StartRequestTimer(int requestTimeout)
        {
            await Task.Delay(requestTimeout);
            SetException(new TimeoutException("Request timed out."));
        }

        protected RawPubKey AlphaPubkey { get; }
        protected RawPubKey[] Auditors { get; }

        protected TaskCompletionSource<MessageEnvelope> acknowledgmentSource = new TaskCompletionSource<MessageEnvelope>();

        protected void AssignResponseToSource(TaskCompletionSource<MessageEnvelope> completionSource, MessageEnvelope envelope)
        {
            var resultMessage = (ResultMessage)envelope.Message;
            if (resultMessage.Status == ResultStatusCodes.Success)
                completionSource.TrySetResult(envelope);
            else
                SetException(new RequestException(envelope, resultMessage.Status.ToString()));
        }

        public Task<MessageEnvelope> AcknowledgmentSource => acknowledgmentSource.Task;

        public virtual Task<MessageEnvelope> ResponseTask => AcknowledgmentSource;

        public bool IsCompleted => ResponseTask.IsCompleted;

        public virtual void AssignResponse(MessageEnvelope envelope)
        {
            if (envelope.Signatures.Count == 1 && envelope.Signatures.All(s => ByteArrayPrimitives.Equals(s.Signer, AlphaPubkey)))
                AssignResponseToSource(acknowledgmentSource, envelope);
            else
                SetException(new RequestException(envelope, "Unknown result"));
        }

        public virtual void SetException(Exception exc)
        {
            acknowledgmentSource.TrySetException(exc);
        }
    }
    public class CentaurusQuantumResponse : CentaurusResponse
    {
        public CentaurusQuantumResponse(ConstellationInfo constellationInfo, int requestTimeout)
            : base(constellationInfo, requestTimeout)
        {
        }

        private TaskCompletionSource<MessageEnvelope> finalizeSource = new TaskCompletionSource<MessageEnvelope>();

        public Task<MessageEnvelope> FinalizeTask => finalizeSource.Task;

        public override Task<MessageEnvelope> ResponseTask => FinalizeTask;

        public override void AssignResponse(MessageEnvelope envelope)
        {
            var resultMessage = (ResultMessage)envelope.Message;
            if (envelope.Signatures.Count == 1 && envelope.Signatures.All(s => ByteArrayPrimitives.Equals(s.Signer, AlphaPubkey)))
            {
                AssignResponseToSource(acknowledgmentSource, envelope);
            }
            else if (envelope.Signatures.Count > 1
                && envelope.HasMajority(Auditors.Length)
                && envelope.Signatures.Distinct().Count() == envelope.Signatures.Count
                && envelope.Signatures.All(s => Auditors.Any(a => s.Signer.Equals(a)) || s.Signer.Equals(AlphaPubkey))
                && (resultMessage is ITransactionResultMessage //TODO: remove it after ITransactionResultMessage refactoring
                    || envelope.AreSignaturesValid()))
            {
                AssignResponseToSource(finalizeSource, envelope);
            }
            else
            {
                var exc = new RequestException(envelope, "Unknown result");
                acknowledgmentSource.TrySetException(exc);
                finalizeSource.TrySetException(exc);
            }
        }

        public override void SetException(Exception exc)
        {
            acknowledgmentSource.TrySetException(exc);
            finalizeSource.TrySetException(exc);
        }
    }
}
