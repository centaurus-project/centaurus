using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain
{
    public class AlphaStateHandler : BaseAuditorMessageHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaStateHandler(AuditorContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AlphaState;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override Task HandleMessage(AuditorWebSocketConnection connection, IncomingMessage message)
        {
            //if we got this message than verification succeeded 
            var alphaState = (AlphaState)message.Envelope.Message;

            logger.Info($"Alpha state is {alphaState.State}");

            connection.ConnectionState = ConnectionState.Ready;
            //send apex cursor message to start receive quanta
            _ = connection.SendMessage(new SetApexCursor() { Apex = Context.QuantumStorage.CurrentApex });

            return Task.CompletedTask;
        }
    }
}