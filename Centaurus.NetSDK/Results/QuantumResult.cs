using System;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.NetSDK
{
    /// <summary>
    /// Message processing result received from the server.
    /// </summary>
    public class QuantumResult
    {
        public QuantumResult(MessageEnvelope requestEnvelope, ConstellationInfo constellationInfo)
        {
            Request = requestEnvelope ?? throw new ArgumentNullException(nameof(requestEnvelope));
            IsFinalizationRequired = requestEnvelope.Message is SequentialRequestMessage;
            ConstellationInfo = constellationInfo ?? throw new ArgumentNullException(nameof(constellationInfo));
        }

        internal readonly TaskCompletionSource<MessageEnvelope> Acknowledged = new TaskCompletionSource<MessageEnvelope>();

        internal readonly TaskCompletionSource<MessageEnvelope> Finalized = new TaskCompletionSource<MessageEnvelope>();

        internal bool IsFinalizationRequired { get; private set; }

        internal ConstellationInfo ConstellationInfo;

        /// <summary>
        /// Event that resolves when the server returns the acknowledgment confirmation for the client request.
        /// </summary>
        public Task<MessageEnvelope> OnAcknowledged => Acknowledged.Task;

        /// <summary>
        /// Event that resolves when the server returns both the acknowledgment and the finalization confirmation for the quantum request.
        /// </summary>
        public Task<MessageEnvelope> OnFinalized => Finalized.Task;

        /// <summary>
        /// Returns <see langword="true"/> when the quantum confirmation response from the server received and <see langword="false"/> otherwise.
        /// </summary>
        public bool IsFinalized => OnFinalized.IsCompleted && OnAcknowledged.IsCompleted;

        /// <summary>
        /// Returns <see langword="true"/> when the acknowledgment response from the server received and <see langword="false"/> otherwise.
        /// </summary>
        public bool IsAcknowledged => OnAcknowledged.IsCompleted;

        /// <summary>
        /// Original request message sent from the client.
        /// </summary>
        public MessageEnvelope Request { get; private set; }

        /// <summary>
        /// Returns a result message envelope once a response received.
        /// </summary>
        /// <remarks>
        /// Calling this method will block current thread until the response from the server is received.
        /// </remarks>
        public virtual MessageEnvelope Result => IsFinalizationRequired ? OnFinalized.Result : OnAcknowledged.Result;

        internal void AssignResponse(MessageEnvelope resultEnvelope)
        {
            if (!(resultEnvelope.Message is ResultMessageBase resultMessage))
                SetException(new RequestException(resultEnvelope, "Received message is not result message."));
            //handle failed quantum case
            else if (resultMessage.Status != ResultStatusCodes.Success)
                SetException(new RequestException(resultEnvelope, resultMessage.Status.ToString()));
            else
                //handle quantum result
                try
                {
                    var quantumValidationResult = ValidateResultMessage(resultEnvelope);
                    if (!quantumValidationResult.isValid)
                        throw new RequestException(resultEnvelope, "Received message is not signed by auditor(s).");
                    if (quantumValidationResult.isFinalized)
                    {
                        if (Finalized.Task.IsCompleted)
                            throw new RequestException(resultEnvelope, "Finalize result message has been already received.");
                        if (!Acknowledged.Task.IsCompleted) //complete acknowledgment task if it's not completed yet
                            Acknowledged.TrySetResult(resultEnvelope);
                        Finalized.TrySetResult(resultEnvelope);
                    }
                    else
                    {
                        if (Acknowledged.Task.IsCompleted)
                            throw new RequestException(resultEnvelope, "Acknowledgment result message has been already received.");
                        Acknowledged.TrySetResult(resultEnvelope);
                    }
                }
                catch (Exception e)
                {
                    SetException(e);
                }
        }

        private (bool isValid, bool isFinalized) ValidateResultMessage(MessageEnvelope resultEnvelope)
        {
            if (resultEnvelope.Message is QuantumResultMessage quantumResult)
            {
                //TODO: cache current auditors keypairs
                var signaturesCount = quantumResult.GetValidSignaturesCount(ConstellationInfo.Auditors.Select(a => new KeyPair(a.PubKey)).ToList());
                if (signaturesCount < 1)
                    throw new RequestException(resultEnvelope, "Result message has no signatures.");

                if (MajorityHelper.HasMajority(signaturesCount, ConstellationInfo.Auditors.Count))
                    return (true, true); //valid and finalized
                else
                    return (true, false); //valid but not finalized
            }
            else
            {
                var isSignedByAuditor = ConstellationInfo.Auditors.Any(a => resultEnvelope.IsSignatureValid(new RawPubKey(a.PubKey)));
                return (isSignedByAuditor, false);
            }
        }

        /// <summary>
        /// Set result exception.
        /// </summary>
        /// <param name="e"></param>
        internal void SetException(Exception e)
        {
            if (!OnAcknowledged.IsCompleted)
            {
                Acknowledged.TrySetException(e);
            }
            if (IsFinalizationRequired && !OnFinalized.IsCompleted)
            {
                Finalized.TrySetException(e);
            }
        }

        /// <summary>
        /// Schedule result expiration on the connection timeout.
        /// </summary>
        /// <param name="requestTimeout"></param>
        internal void ScheduleExpiration(int requestTimeout)
        {
#if !DEBUG
            Task.Delay(requestTimeout).ContinueWith(task => SetException(new TimeoutException("Request timed out.")));
#endif
        }
    }
}
