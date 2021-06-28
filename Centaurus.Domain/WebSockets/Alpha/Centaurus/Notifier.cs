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
        /// <param name="envelope">Message to send</param>
        public static void Notify(this ExecutionContext context, RawPubKey account, MessageEnvelope envelope)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            context.ExtensionsManager.BeforeNotify(account, envelope);
            if (context.ConnectionManager.TryGetConnection(account, out IncomingWebSocketConnection connection))
                Task.Factory.StartNew(async () => await connection.SendMessage(envelope)).Unwrap();
        }

        /// <summary>
        /// Sends the message to all connected auditors
        /// </summary>
        /// <param name="envelope">Message to send</param>
        public static void NotifyAuditors(this ExecutionContext context, MessageEnvelope envelope)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            context.ExtensionsManager.BeforeNotifyAuditors(envelope);
            var auditors = context.ConnectionManager.GetAuditorConnections();
            for (var i = 0; i < auditors.Count; i++)
            {
                var auditor = auditors[i];
                Task.Factory.StartNew(async () => await auditor.SendMessage(envelope)).Unwrap();
            }
        }

        /// <summary>
        /// Notifies message author(s) about message processing result
        /// </summary>
        /// <param name="result">Result message</param>
        public static void OnMessageProcessResult(this ExecutionContext context, ResultMessage result)
        {
            if (result == null)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var signatures = result.OriginalMessage.Signatures;

            //unwrap if it is RequestQuantum
            if (result.OriginalMessage.Message is RequestQuantum)
                signatures = ((RequestQuantum)result.OriginalMessage.Message).RequestEnvelope.Signatures;
            var envelope = result.CreateEnvelope();
            for (var i = 0; i < signatures.Count; i++)
            {
                var accountToNotify = signatures[i];
                context.Notify(accountToNotify.Signer, envelope);
            }
        }
    }
}
