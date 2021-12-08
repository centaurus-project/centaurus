using Centaurus.Models;
using NLog;
using System;
using System.Timers;

namespace Centaurus.Domain
{

    internal class StateNotifierWorker : ContextualBase
    {
        private Logger logger = LogManager.GetCurrentClassLogger();

        public StateNotifierWorker(ExecutionContext context)
            : base(context)
        {
            InitBroadcastTimer();
        }

        private Timer broadcastTimer;

        private void InitBroadcastTimer()
        {
            broadcastTimer = new Timer();
            broadcastTimer.AutoReset = false;
            broadcastTimer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            broadcastTimer.Elapsed += BroadcastTimer_Elapsed;
            broadcastTimer.Start();
        }

        private void BroadcastTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var currentNode = Context.NodesManager.CurrentNode;
                var currentApex = Context.QuantumHandler.CurrentApex;
                var lastPersistedApex = Context.PendingUpdatesManager.LastPersistedApex;
                var quantaQueueLenght = Context.QuantumHandler.QuantaQueueLenght;
                var updateDate = DateTime.UtcNow;
                var updateMessage = new StateMessage
                {
                    State = currentNode.State,
                    CurrentApex = currentApex,
                    LastPersistedApex = lastPersistedApex,
                    QuantaQueueLength = quantaQueueLenght,
                    UpdateDate = updateDate
                };
                currentNode.UpdateData(currentApex, lastPersistedApex, quantaQueueLenght, updateDate);
                Context.NotifyAuditors(updateMessage.CreateEnvelope<MessageEnvelopeSignless>());
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on the state broadcasting.");
            }
            finally
            {
                broadcastTimer.Start();
            }
        }
    }
}