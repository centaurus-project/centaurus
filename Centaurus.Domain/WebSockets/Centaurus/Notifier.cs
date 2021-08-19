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
        public static void Notify(this ExecutionContext context, RawPubKey account, MessageEnvelopeBase envelope)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            context.ExtensionsManager.BeforeNotify(account, envelope);
            if (context.IncomingConnectionManager.TryGetConnection(account, out IncomingConnectionBase connection))
                Task.Factory.StartNew(async () => await connection.SendMessage(envelope)).Unwrap();
        }

        /// <summary>
        /// Sends the message to all connected auditors
        /// </summary>
        /// <param name="envelope">Message to send</param>
        public static void NotifyAuditors(this ExecutionContext context, MessageEnvelopeBase envelope)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            context.ExtensionsManager.BeforeNotifyAuditors(envelope);
            var auditors = context.IncomingConnectionManager.GetAuditorConnections();
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
        public static void OnMessageProcessResult(this ExecutionContext context, ResultMessageBase result, RawPubKey rawPubKey)
        {
            if (result == null)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (rawPubKey == null)
                throw new ArgumentNullException(nameof(rawPubKey));
            
            context.Notify(rawPubKey, result.CreateEnvelope());
        }
    }
}
