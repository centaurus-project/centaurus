using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Centaurus.Models;
using NLog;

namespace Centaurus.NetSDK
{
    public class MessageCollator: IEnumerable<QuantumResult>
    {
        private readonly ConcurrentDictionary<long, QuantumResult> Requests = new ConcurrentDictionary<long, QuantumResult>();

        public void Add(QuantumResult response)
        {
            if (!Requests.TryAdd(response.Request.Message.MessageId, response))
                throw new Exception("Failed to schedule request collation.");
        }

        public bool Resolve(MessageEnvelopeBase envelope, out QuantumResult quantumResult)
        {
            quantumResult = null;
            if (!(envelope.Message is ResultMessageBase resultMessage))
            {
                logger.Trace("Request is not a quantum result message.");
                return false;
            }
            var messageId = resultMessage.OriginalMessageId;
            if (!Requests.TryGetValue(messageId, out quantumResult))
            {
                logger.Trace($"Unable set result for msg with id {envelope.Message.GetMessageType()}:{messageId}.");
                return false;
            }
            quantumResult.AssignResponse(envelope);
            if (quantumResult.IsFinalized)
                Requests.TryRemove(messageId, out _);

            logger.Trace($"{envelope.Message.GetMessageType()}:{messageId} result was set.");
            return true;
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public IEnumerator<QuantumResult> GetEnumerator()
        {
            return Requests.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
