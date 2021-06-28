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
    public abstract class CentaurusResponseBase
    {
        public abstract Task<MessageEnvelope> ResponseTask { get; }

        public bool IsCompleted => ResponseTask.IsCompleted;
    }

    public class VoidResponse : CentaurusResponseBase
    {
        public override Task<MessageEnvelope> ResponseTask => Task.FromResult(default(MessageEnvelope));
    }

    public class CentaurusResponse : CentaurusResponseBase
    {
        public CentaurusResponse(RawPubKey serverKey, List<RawPubKey> auditors, int requestTimeout)
        {
            ServerPubkey = serverKey;
            Auditors = auditors;
            _ = StartRequestTimer(requestTimeout);
        }


        async Task StartRequestTimer(int requestTimeout)
        {
            await Task.Delay(requestTimeout);
            SetException(new TimeoutException("Request timed out."));
        }

        protected RawPubKey ServerPubkey { get; }
        protected List<RawPubKey> Auditors { get; }

        protected TaskCompletionSource<MessageEnvelope> acknowledgmentSource = new TaskCompletionSource<MessageEnvelope>();

        protected void AssignResponseToSource(TaskCompletionSource<MessageEnvelope> completionSource, MessageEnvelope envelope)
        {
            var resultMessage = (ResultMessage)envelope.Message;
            if (resultMessage.Status == ResultStatusCodes.Success)
                completionSource.TrySetResult(envelope);
            else
                SetException(new RequestException(envelope, resultMessage.Status.ToString()));
        }


        protected void ValidateSignatures(MessageEnvelope envelope)
        {
            if (envelope.Signatures.Count == 0 || !envelope.Signatures[0].Signer.Equals(ServerPubkey) || !envelope.AreSignaturesValid())
                throw new RequestException(envelope, "Invalid signature.");
        }


        public Task<MessageEnvelope> AcknowledgmentTask => acknowledgmentSource.Task;

        public override Task<MessageEnvelope> ResponseTask => AcknowledgmentTask;

        public virtual void AssignResponse(MessageEnvelope envelope)
        {
            try
            {
                ValidateSignatures(envelope);
                AssignResponseToSource(acknowledgmentSource, envelope);
            }
            catch (Exception exc)
            {
                SetException(exc);
            }
        }

        public virtual void SetException(Exception exc)
        {
            if (IsCompleted)
                return;
            acknowledgmentSource.TrySetException(exc);
        }
    }

    public class CentaurusQuantumResponse : CentaurusResponse
    {
        public CentaurusQuantumResponse(RawPubKey alphaPubKey, List<RawPubKey> auditors, int requestTimeout)
            : base(alphaPubKey, auditors, requestTimeout)
        {
        }

        private TaskCompletionSource<MessageEnvelope> finalizeSource = new TaskCompletionSource<MessageEnvelope>();

        public Task<MessageEnvelope> FinalizeTask => finalizeSource.Task;

        public override Task<MessageEnvelope> ResponseTask => FinalizeTask;

        public override void AssignResponse(MessageEnvelope envelope)
        {
            try
            {

                ValidateSignatures(envelope);

                var resultMessage = (QuantumResultMessage)envelope.Message;

                var effectsProof = resultMessage.Effects;
                if (effectsProof.Signatures.Count < 1)
                    throw new RequestException(envelope, "Result message has no signatures.");
                if (effectsProof.Signatures.Distinct().Count() != envelope.Signatures.Count)
                    throw new RequestException(envelope, "Duplicate signatures.");
                if (!effectsProof.Signatures.All(s => Auditors.Any(a => s.Signer.Equals(a))))
                    throw new RequestException(envelope, "Unknown signer.");
                if (!(resultMessage is ITransactionResultMessage || effectsProof.Signatures.AreSignaturesValid(effectsProof.Hashes.ComputeHash())))//TODO: remove it after ITransactionResultMessage refactoring
                    throw new RequestException(envelope, "At least one signature is invalid.");

                if (MajorityHelper.HasMajority(effectsProof.Signatures.Count, Auditors.Count))
                {
                    if (finalizeSource.Task.IsCompleted)
                        throw new RequestException(envelope, "Finalize result message was already received.");
                    if (!acknowledgmentSource.Task.IsCompleted) //complete acknowledgment task if it's not completed yet
                        AssignResponseToSource(acknowledgmentSource, envelope);
                    AssignResponseToSource(finalizeSource, envelope);
                }
                else
                {
                    if (acknowledgmentSource.Task.IsCompleted)
                        throw new RequestException(envelope, "Acknowledgment result message was already received.");
                    AssignResponseToSource(acknowledgmentSource, envelope);
                }
            }
            catch (Exception exc)
            {
                acknowledgmentSource.TrySetException(exc);
                finalizeSource.TrySetException(exc);
            }
        }

        public override void SetException(Exception exc)
        {
            if (IsCompleted)
                return;
            acknowledgmentSource.TrySetException(exc);
            finalizeSource.TrySetException(exc);
        }
    }
}
