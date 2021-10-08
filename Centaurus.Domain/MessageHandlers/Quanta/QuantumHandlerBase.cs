﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    /// <summary>
    /// This handler should handle all quantum requests
    /// </summary>
    public abstract class QuantumHandlerBase : MessageHandlerBase
    {
        protected QuantumHandlerBase(ExecutionContext context)
            : base(context)
        {
        }

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            if (Context.IsAlpha)
            {
                Context.QuantumHandler.HandleAsync(GetQuantum(connection, message), Task.FromResult(true));
            }
            else
            {
                //TODO: send it to Alpha
            }
            return Task.CompletedTask;
        }

        protected virtual Quantum GetQuantum(ConnectionBase connection, IncomingMessage message)
        {
            return new RequestQuantum { RequestEnvelope = message.Envelope };
        }
    }
}
