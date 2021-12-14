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
    internal abstract class QuantumHandlerBase : MessageHandlerBase
    {
        protected QuantumHandlerBase(ExecutionContext context)
            : base(context)
        {
        }

        public override bool IsAuthenticatedOnly => true;

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            if (Context.NodesManager.IsAlpha)
            {
                Context.QuantumHandler.HandleAsync(RequestQuantumHelper.GetQuantum(message.Envelope), Task.FromResult(true));
            }
            else
            {
                Context.ProxyWorker.AddRequestsToQueue(message.Envelope);
            }
            return Task.CompletedTask;
        }
    }
}
