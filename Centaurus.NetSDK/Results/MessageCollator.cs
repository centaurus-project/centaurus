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

        public void Resolve(MessageEnvelope envelope)
        {
            if (!(envelope.Message is ResultMessageBase resultMessage))
            {
                logger.Trace("Request is not a quantum result message.");
                return;
            }
            var messageId = resultMessage.OriginalMessage.Message is RequestQuantum requestQuantum ?
                requestQuantum.RequestMessage.MessageId :
                resultMessage.OriginalMessage.Message.MessageId;
            if (!Requests.TryGetValue(messageId, out var response))
            {
                logger.Trace($"Unable set result for msg with id {envelope.Message.MessageType}:{messageId}.");
                return;
            }
            response.AssignResponse(envelope);
            if (response.IsFinalized)
            {
                Requests.TryRemove(messageId, out _);
            }

            logger.Trace($"{envelope.Message.MessageType}:{messageId} result was set.");
            return;
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
