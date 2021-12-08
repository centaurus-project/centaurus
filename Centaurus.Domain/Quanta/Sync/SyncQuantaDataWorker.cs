using Centaurus.Domain.Quanta.Sync;
using Centaurus.Domain.StateManagers;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal partial class SyncQuantaDataWorker : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public SyncQuantaDataWorker(ExecutionContext context)
            : base(context)
        {
            Task.Factory.StartNew(SyncQuantaData);
        }

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private bool GetIsCurrentNodeReady()
        {
            var currentNode = Context.NodesManager.CurrentNode;
            return currentNode.State == State.Running || currentNode.State == State.Ready;
        }

        private List<CursorGroup> GetCursors()
        {
            var nodes = Context.NodesManager.GetRemoteNodes();
            var cursors = new Dictionary<string, CursorGroup>();
            foreach (var node in nodes)
            {
                if (!node.IsReadyToHandleQuanta())
                    continue;
                var nodeCursors = node.GetActiveCursors();
                SetNodeCursors(cursors, node, nodeCursors);
            }

            return cursors.Values.ToList();
        }

        private void SetNodeCursors(Dictionary<string, CursorGroup> cursors, RemoteNode node, List<RemoteNodeCursor> nodeCursors)
        {
            foreach (var cursor in nodeCursors)
                SetCursors(cursors, node, cursor.CursorType, cursor.Cursor, cursor.UpdateDate);
        }

        private void SetCursors(Dictionary<string, CursorGroup> cursors, RemoteNode node, SyncCursorType cursorType, ulong cursor, DateTime timeToken)
        {
            var cursorToLookup = cursor + 1;
            var batchId = cursorToLookup - cursorToLookup % (ulong)Context.SyncStorage.PortionSize;
            var groupId = $"{cursorType}-{batchId}";
            if (!cursors.TryGetValue(groupId, out var currentCursorGroup))
            {
                currentCursorGroup = new CursorGroup(cursorType, batchId);
                cursors.Add(groupId, currentCursorGroup);
            }
            currentCursorGroup.Nodes.Add(new NodeCursorData(node, timeToken, cursor));
            if (currentCursorGroup.LastUpdate == default || currentCursorGroup.LastUpdate > timeToken)
                currentCursorGroup.LastUpdate = timeToken;
        }

        const int ForceTimeOut = 100;

        private List<Task<NodeSyncCursorUpdate>> SendQuantaData(List<CursorGroup> cursorGroups)
        {
            var sendingQuantaTasks = new List<Task<NodeSyncCursorUpdate>>();

            foreach (var cursorGroup in cursorGroups)
            {
                var cursorSendingTasks = ProcessCursorGroup(cursorGroup);
                if (cursorSendingTasks.Count > 0)
                    sendingQuantaTasks.AddRange(cursorSendingTasks);
            }
            return sendingQuantaTasks;
        }

        private List<Task<NodeSyncCursorUpdate>> ProcessCursorGroup(CursorGroup cursorGroup)
        {
            if (!(ValidateCursorGroup(cursorGroup) && TryGetBatch(cursorGroup, out var batch)))
                return null;

            var currentCursor = cursorGroup.BatchId;
            var cursorType = cursorGroup.CursorType;

            var lastBatchApex = batch.LastDataApex;

            var sendingQuantaTasks = new List<Task<NodeSyncCursorUpdate>>();
            foreach (var node in cursorGroup.Nodes)
            {
                var sendMessageTask = SendSingleBatch(batch, currentCursor, cursorType, node);
                if (sendMessageTask != null)
                    sendingQuantaTasks.Add(sendMessageTask);
            }
            return sendingQuantaTasks;
        }

        private static Task<NodeSyncCursorUpdate> SendSingleBatch(SyncPortion batch, ulong currentCursor, SyncCursorType cursorType, NodeCursorData currentAuditor)
        {
            var connection = currentAuditor.Node.GetConnection();
            if (currentAuditor.Cursor < batch.LastDataApex || connection == null)
                return null;
            var sendMessageTask = connection.SendMessage(batch.Data.AsMemory());
            return sendMessageTask.ContinueWith(t =>
            {
                if (!t.IsFaulted)
                {
                    HandleFaultedSendTask(t, currentCursor, batch, cursorType, currentAuditor);
                    return null;
                }
                return new NodeSyncCursorUpdate(currentAuditor.Node,
                    new SyncCursorUpdate(currentAuditor.TimeToken, batch.LastDataApex, cursorType)
                );
            });
        }

        private bool TryGetBatch(CursorGroup cursorGroup, out SyncPortion batch)
        {
            var force = (DateTime.UtcNow - cursorGroup.LastUpdate).TotalMilliseconds > ForceTimeOut;

            switch (cursorGroup.CursorType)
            {
                case SyncCursorType.Quanta:
                    batch = Context.SyncStorage.GetQuanta(cursorGroup.BatchId, force);
                    break;
                case SyncCursorType.Signatures:
                    batch = Context.SyncStorage.GetSignatures(cursorGroup.BatchId, force);
                    break;
                default:
                    throw new NotImplementedException($"{cursorGroup.CursorType} cursor type is not supported.");
            }
            return batch != null;
        }

        private bool ValidateCursorGroup(CursorGroup cursorGroup)
        {
            var currentCursor = cursorGroup.BatchId;
            if (currentCursor == Context.QuantumHandler.CurrentApex)
                return false;
            if (currentCursor > Context.QuantumHandler.CurrentApex && GetIsCurrentNodeReady())
            {
                var message = $"Auditors {string.Join(',', cursorGroup.Nodes.Select(a => a.Node.AccountId))} is above current constellation state.";
                if (cursorGroup.Nodes.Count >= Context.GetMajorityCount() - 1) //-1 is current server
                    logger.Error(message);
                else
                    logger.Info(message);
                return false;
            }
            return true;
        }

        private static void HandleFaultedSendTask(Task t, ulong currentCursor, SyncPortion batch, SyncCursorType cursorType, NodeCursorData currentAuditor)
        {
            logger.Error(t.Exception, $"Unable to send quanta data to {currentAuditor.Node.AccountId}. CursorType: {cursorType}; Cursor: {currentCursor}; CurrentApex: {batch.LastDataApex}");
        }

        private async Task SyncQuantaData()
        {
            var hasPendingQuanta = true;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!hasPendingQuanta)
                    Thread.Sleep(50);

                if (!Context.IsAlpha || !GetIsCurrentNodeReady())
                {
                    hasPendingQuanta = false;
                    continue;
                }

                hasPendingQuanta = await TrySendData();
            }
        }

        private async Task<bool> TrySendData()
        {
            var hasPendingQuanta = false;
            try
            {
                var cursors = GetCursors();

                var sendingQuantaTasks = SendQuantaData(cursors);
                hasPendingQuanta = await HandleSendingTasks(sendingQuantaTasks);
            }
            catch (Exception exc)
            {
                if (exc is ObjectDisposedException
                || exc.GetBaseException() is ObjectDisposedException)
                    throw;
                logger.Error(exc, "Error on quanta data sync.");
            }

            return hasPendingQuanta;
        }

        private static async Task<bool> HandleSendingTasks(List<Task<NodeSyncCursorUpdate>> sendingQuantaTasks)
        {
            var hasPendingQuanta = false;
            if (sendingQuantaTasks.Count < 1)
                return false;

            var cursorUpdates = await Task.WhenAll(sendingQuantaTasks);
            foreach (var cursorUpdate in cursorUpdates)
            {
                if (cursorUpdate == null)
                    continue;
                cursorUpdate.Node.SetCursor(cursorUpdate.CursorUpdate.CursorType, cursorUpdate.CursorUpdate.TimeToken, cursorUpdate.CursorUpdate.NewCursor);
                hasPendingQuanta = true;
            }

            return hasPendingQuanta;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}