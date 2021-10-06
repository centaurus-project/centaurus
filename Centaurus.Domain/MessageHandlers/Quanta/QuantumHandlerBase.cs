using System;
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

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            if (Context.IsAlpha)
            {
                try
                {
                    var processingItem = Context.QuantumHandler.HandleAsync(GetQuantum(connection, message), Task.FromResult(true));
                    //wait for processing
                    await processingItem.OnAcknowledge;
                    Task.WaitAny(new Task[] { processingItem.OnFinalized }, 150); //give a chance to get finalization
                    var isFinalized = processingItem.OnFinalized.IsCompleted;
                    await SendResult(connection, processingItem);
                    if (!isFinalized)
                        _ = ScheduleFinalizeNotification(connection, processingItem);
                }
                catch (Exception exc)
                {
                    await connection.SendMessage(message.Envelope.CreateResult(exc).CreateEnvelope());
                }
            }
            else
            {
                //TODO: send it to Alpha
            }
        }

        private async Task SendResult(ConnectionBase connection, QuantumProcessingItem processingItem)
        {
            await connection.SendMessage(processingItem.ResultMessage.CreateEnvelope<MessageEnvelopeSignless>());
        }

        private async Task ScheduleFinalizeNotification(ConnectionBase connection, QuantumProcessingItem processingItem)
        {
            await processingItem.OnFinalized
                .ContinueWith(async t =>
                {
                    if (t.IsCompletedSuccessfully)
                        await SendResult(connection, processingItem);
                    else
                        await connection.SendMessage(new ResultMessage
                        {
                            ErrorMessage = "Error on getting finalization.",
                            OriginalMessageId = processingItem.ResultMessage.OriginalMessageId,
                            Status = ResultStatusCode.InternalError
                        });
                }).Unwrap();
        }

        protected virtual Quantum GetQuantum(ConnectionBase connection, IncomingMessage message)
        {
            return new RequestQuantum { RequestEnvelope = message.Envelope };
        }
    }
}
