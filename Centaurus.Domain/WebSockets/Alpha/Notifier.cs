using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class Notifier
    {
        /// <summary>
        /// Sends the message to the account
        /// </summary>
        /// <param name="account">Target account</param>
        /// <param name="message">Message to send</param>
        public static void Notify(RawPubKey account, Message message)
        {
            if (ConnectionManager.TryGetConnection(account, out AlphaWebSocketConnection connection))
                _ = connection.SendMessage(message);
        }

        /// <summary>
        /// Sends the message to the account
        /// </summary>
        /// <param name="account">Target account</param>
        /// <param name="envelope">Message to send</param>
        public static void Notify(RawPubKey account, MessageEnvelope envelope)
        {
            if (ConnectionManager.TryGetConnection(account, out AlphaWebSocketConnection connection))
                _ = connection.SendMessage(envelope);
        }

        /// <summary>
        /// Sends the message to all connected auditors
        /// </summary>
        /// <param name="message">Message to send</param>
        public static void NotifyAuditors(Message message)
        {
            var auditors = ConnectionManager.GetAuditorConnections();
            for (var i = 0; i < auditors.Count; i++)
                _ = auditors[i].SendMessage(message);
        }

        /// <summary>
        /// Sends the message to all connected auditors
        /// </summary>
        /// <param name="envelope">Message to send</param>
        public static void NotifyAuditors(MessageEnvelope envelope)
        {
            var auditors = ConnectionManager.GetAuditorConnections();
            for (var i = 0; i < auditors.Count; i++)
                _ = auditors[i].SendMessage(envelope);
        }

        /// <summary>
        /// Notifies message author(s) about message processing result
        /// </summary>
        /// <param name="result">Result message</param>
        public static void OnMessageProcessResult(ResultMessage result)
        {
            if (result == null)
                return;

            var signatures = result.OriginalMessage.Signatures;

            //unwrap if it is RequestQuantum
            if (result.OriginalMessage.Message is RequestQuantum)
                signatures = ((RequestQuantum)result.OriginalMessage.Message).RequestEnvelope.Signatures;

            for (var i = 0; i < signatures.Count; i++)
            {
                var accountToNotify = signatures[i];
                Notify(accountToNotify.Signer, result);
            }
        }
    }
}
