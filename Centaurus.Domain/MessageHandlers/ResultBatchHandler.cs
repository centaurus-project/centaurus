﻿using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ResultBatchHandler : MessageHandlerBase
    {
        public ResultBatchHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(AuditorResultsBatch).Name;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = true;

        //TODO: run result aggregation in separate thread
        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var resultsBatch = (AuditorResultsBatch)message.Envelope.Message;
            foreach (var result in resultsBatch.AuditorResultMessages)
                Context.AuditResultManager.Add(result, connection.PubKey);
            return Task.CompletedTask;
        }
    }
}