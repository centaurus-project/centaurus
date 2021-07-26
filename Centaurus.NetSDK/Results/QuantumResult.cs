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
            Request = requestEnvelope;
            IsFinalizationRequired = requestEnvelope.Message is SequentialRequestMessage;
            ConstellationInfo = constellationInfo;
        }

        internal readonly TaskCompletionSource<MessageEnvelope> Acknowledged = new TaskCompletionSource<MessageEnvelope>();

        internal readonly TaskCompletionSource<MessageEnvelope> Finalized = new TaskCompletionSource<MessageEnvelope>();

        internal bool IsFinalizationRequired { get; private set; }

        internal ConstellationInfo ConstellationInfo;

        /// <summary>
        /// Event that resolves when the server returns the acknowledgement confirmation for the client request.
        /// </summary>
        public Task<MessageEnvelope> OnAcknowledged => Acknowledged.Task;

        /// <summary>
        /// Event that resolves when the server returns both the acknowledgement and the finalization confirmation for the quantum request.
        /// </summary>
        public Task<MessageEnvelope> OnFinalized => Finalized.Task;

        /// <summary>
        /// Returns <see langword="true"/> when the quantum confirmation response from the server received and <see langword="false"/> otherwise.
        /// </summary>
        public bool IsFinalized => OnFinalized.IsCompleted && OnAcknowledged.IsCompleted;

        /// <summary>
        /// Returns <see langword="true"/> when the acknowledgement response from the server received and <see langword="false"/> otherwise.
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
            var resultMessage = resultEnvelope.Message as ResultMessage;
            //handle failed quantum case
            if (resultMessage.Status != ResultStatusCodes.Success)
            {
                SetException(new RequestException(resultEnvelope, resultMessage.Status.ToString()));
                return;
            }

            //handle acknowledgement
            if (resultEnvelope.Signatures.Count == 1 &&
                ByteArrayPrimitives.Equals(resultEnvelope.Signatures[0].Signer, ConstellationInfo.VaultPubKey))
            { 
                Acknowledged.TrySetResult(resultEnvelope);
                return;
            }
            //handle quantum result
            try
            {
                if (resultEnvelope.Signatures.Count < 1)
                    throw new RequestException(resultEnvelope, "Result message has no signatures.");
                if (resultEnvelope.Signatures.Distinct().Count() != resultEnvelope.Signatures.Count)
                    throw new RequestException(resultEnvelope, "Duplicate signatures.");
                if (!resultEnvelope.Signatures.Any(s => s.Signer.Equals(ConstellationInfo.VaultPubKey)))
                    throw new RequestException(resultEnvelope, "Result message has not been signed by Alpha.");
                if (!resultEnvelope.Signatures.All(s => ConstellationInfo.AuditorPubKeys.Any(a => s.Signer.Equals(a)) || s.Signer.Equals(ConstellationInfo.VaultPubKey)))
                    throw new RequestException(resultEnvelope, "Unknown signer.");
                if (!(resultMessage is ITransactionResultMessage || resultEnvelope.AreSignaturesValid()))//TODO: remove it after ITransactionResultMessage refactoring
                    throw new RequestException(resultEnvelope, "At least one signature is invalid.");

                if (resultEnvelope.HasMajority(ConstellationInfo.AuditorPubKeys.Length))
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
                Acknowledged.TrySetException(e);
                Finalized.TrySetException(e);
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
            Task.Delay(requestTimeout).ContinueWith(task => SetException(new TimeoutException("Request timed out."))).Start();
        }
    }
}
