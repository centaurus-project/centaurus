using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class WithdrawalMessageHandler : QuantumHandlerBase
    {
        public WithdrawalMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(WithdrawalRequest).Name;

        protected override Task HandleQuantum(ConnectionBase connection, IncomingMessage message)
        {
            Context.QuantumHandler.HandleAsync(new WithdrawalRequestQuantum { RequestEnvelope = message.Envelope });
            return Task.CompletedTask;
        }
    }
}
