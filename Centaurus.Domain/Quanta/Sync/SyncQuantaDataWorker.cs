using Centaurus.Domain.Quanta.Sync;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class SyncQuantaDataWorker : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public SyncQuantaDataWorker(ExecutionContext context)
            : base(context)
        {
            Task.Factory.StartNew(SyncQuantaData);
        }

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


        private bool IsAuditorReadyToHandleQuanta(IncomingAuditorConnection auditor)
        {
            return auditor.ConnectionState == ConnectionState.Ready //connection is validated
                && (auditor.AuditorState.IsRunning || auditor.AuditorState.IsWaitingForInit); //auditor is ready to handle quanta
        }

        private bool IsCurrentNodeReady => Context.StateManager.State != State.Rising
            && Context.StateManager.State != State.Undefined
            && Context.StateManager.State != State.Failed;

        private List<AuditorCursorGroup> GetCursors()
        {
            var auditorConnections = Context.IncomingConnectionManager.GetAuditorConnections();

            var quantaCursors = new Dictionary<ulong, AuditorCursorGroup>();
            var signaturesCursors = new Dictionary<ulong, AuditorCursorGroup>();
            foreach (var connection in auditorConnections)
            {
                var (quantaCursor, signaturesCursor, timeToken) = connection.GetCursors();
                SetCursors(quantaCursors, connection, SyncCursorType.Quanta, quantaCursor, timeToken);
                SetCursors(signaturesCursors, connection, SyncCursorType.Signatures, signaturesCursor, timeToken);
            }

            return quantaCursors.Values.Union(signaturesCursors.Values).ToList();
        }

        private void SetCursors(Dictionary<ulong, AuditorCursorGroup> cursors, IncomingAuditorConnection connection, SyncCursorType cursorType, ulong? cursor, DateTime timeToken)
        {
            if (cursor.HasValue && cursor.Value < Context.QuantumHandler.CurrentApex)
            {
                var cursorToLookup = cursor.Value + 1;
                var quantaCursor = cursorToLookup - cursorToLookup % (ulong)Context.SyncStorage.PortionSize;
                if (!cursors.TryGetValue(cursorToLookup, out var currentCursorGroup))
                {
                    currentCursorGroup = new AuditorCursorGroup(cursorType, quantaCursor);
                    cursors.Add(cursorToLookup, currentCursorGroup);
                }
                currentCursorGroup.Auditors.Add(new AuditorCursorGroup.AuditorCursorData(connection, timeToken, cursor.Value));
                if (currentCursorGroup.LastUpdate == default || currentCursorGroup.LastUpdate > timeToken)
                    currentCursorGroup.LastUpdate = timeToken;
            }
        }

        const int ForceTimeOut = 100;

        private List<Task<AuditorSyncCursorUpdate>> SendQuantaData(List<AuditorCursorGroup> cursorGroups)
        {
            var sendingQuantaTasks = new List<Task<AuditorSyncCursorUpdate>>();
            foreach (var cursorGroup in cursorGroups)
            {
                var currentCursor = cursorGroup.Cursor;
                if (currentCursor == Context.QuantumHandler.CurrentApex)
                    continue;
                if (currentCursor > Context.QuantumHandler.CurrentApex && IsCurrentNodeReady)
                {
                    var message = $"Auditors {string.Join(',', cursorGroup.Auditors.Select(a => a.Connection.PubKeyAddress))} is above current constellation state.";
                    if (cursorGroup.Auditors.Count >= Context.GetMajorityCount() - 1) //-1 is current server
                        logger.Error(message);
                    else
                        logger.Info(message);
                    continue;
                }

                var force = (DateTime.UtcNow - cursorGroup.LastUpdate).TotalMilliseconds > ForceTimeOut;

                var batch = default(SyncPortion);
                var cursorType = cursorGroup.CursorType;
                switch (cursorType)
                {
                    case SyncCursorType.Quanta:
                        batch = Context.SyncStorage.GetQuanta(currentCursor, force);
                        break;
                    case SyncCursorType.Signatures:
                        batch = Context.SyncStorage.GetSignatures(currentCursor, force);
                        break;
                    default:
                        throw new NotImplementedException($"{cursorType} cursor type is not supported.");
                }

                if (batch == null)
                    continue;

                var lastBatchApex = batch.LastDataApex;

                foreach (var auditorConnection in cursorGroup.Auditors)
                {
                    var currentAuditor = auditorConnection;
                    if (IsAuditorReadyToHandleQuanta(currentAuditor.Connection) && auditorConnection.Cursor < lastBatchApex)
                        sendingQuantaTasks.Add(
                            currentAuditor.Connection.SendMessage(batch.Data.AsMemory())
                                .ContinueWith(t =>
                                {
                                    if (t.IsFaulted)
                                    {
                                        logger.Error(t.Exception, $"Unable to send quanta data to {currentAuditor.Connection.PubKeyAddress}. Cursor: {currentCursor}; CurrentApex: {Context.QuantumHandler.CurrentApex}");
                                        return null;
                                    }
                                    return new AuditorSyncCursorUpdate(currentAuditor.Connection, 
                                        new SyncCursorUpdate(currentAuditor.TimeToken, (ulong?)lastBatchApex, cursorType)
                                    );
                                })
                        );
                }
            }
            return sendingQuantaTasks;
        }

        private async Task SyncQuantaData()
        {
            var hasPendingQuanta = true;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!hasPendingQuanta)
                    Thread.Sleep(50);

                if (!Context.IsAlpha || !IsCurrentNodeReady)
                {
                    hasPendingQuanta = false;
                    continue;
                }

                try
                {
                    var cursors = GetCursors();

                    var sendingQuantaTasks = SendQuantaData(cursors);

                    if (sendingQuantaTasks.Count > 0)
                    {
                        hasPendingQuanta = true;

                        var cursorUpdates = await Task.WhenAll(sendingQuantaTasks);
                        var auditorUpdates = cursorUpdates.Where(u => u != null).GroupBy(u => u.Connection);
                        foreach (var connection in auditorUpdates)
                            connection.Key.SetSyncCursor(false, connection.Select(u => u.CursorUpdate).ToArray());
                    }
                }
                catch (Exception exc)
                {
                    if (exc is ObjectDisposedException
                    || exc.GetBaseException() is ObjectDisposedException)
                        throw;
                    logger.Error(exc, "Error on quanta data sync.");
                }
            }
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        class AuditorCursorGroup
        {
            public AuditorCursorGroup(SyncCursorType cursorType, ulong cursor)
            {
                CursorType = cursorType;
                Cursor = cursor;
            }

            public SyncCursorType CursorType { get; }

            public ulong Cursor { get; }

            public DateTime LastUpdate { get; set; }

            public List<AuditorCursorData> Auditors { get; } = new List<AuditorCursorData>();

            public class AuditorCursorData
            {
                public AuditorCursorData(IncomingAuditorConnection connection, DateTime timeToken, ulong cursor)
                {
                    Connection = connection;
                    TimeToken = timeToken;
                    Cursor = cursor;
                }

                public IncomingAuditorConnection Connection { get; }

                public DateTime TimeToken { get; }

                public ulong Cursor { get; }
            }
        }
    }
}