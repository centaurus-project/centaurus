using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain.Nodes
{
    internal class SyncSourceManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        public SyncSourceManager(ExecutionContext context)
            : base(context)
        {
            InitTimer();
        }

        Timer syncSourceSwitchTimer = new Timer();
        private void InitTimer()
        {
            syncSourceSwitchTimer.AutoReset = false;
            syncSourceSwitchTimer.Interval = TimeSpan.FromSeconds(2).TotalMilliseconds;
            syncSourceSwitchTimer.Elapsed += SyncSourceSwitchTimer_Elapsed;
            syncSourceSwitchTimer.Start();
        }

        private void SyncSourceSwitchTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                ChooseNewSyncNode();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
            finally
            {
                syncSourceSwitchTimer.Start();
            }
        }

        public RemoteNode Source { get; private set; }

        private object syncRoot = new { };

        private void ChooseNewSyncNode()
        {
            lock (syncRoot)
            {
                if (Context.NodesManager.IsAlpha)
                {
                    ClearCurrentSyncCursor();
                    return;
                }

                //first try to connect to prime nodes
                var candidateNode = Context.NodesManager.GetRemoteNodes()
                    .Where(n => n.IsConnected) //only connected
                    .OrderByDescending(n => n.IsAlpha) //first of all try to set alpha
                    .ThenByDescending(n => n.LastApex)
                    .FirstOrDefault();

                if (candidateNode == null)
                    return;
                SetSyncSource(candidateNode);
            }
        }

        private bool ShouldUpdateSyncSource(RemoteNode node)
        {
            if (Source == null)
                return true;

            return node.LastApex > Source.LastApex //if the candidate node is ahead
                && node.LastApex - Source.LastApex > 1000; //and the apexes difference greater than 1000
        }

        private void SetSyncSource(RemoteNode node)
        {
            ClearCurrentSyncCursor();
            SetCurrentSyncCursor(node);
        }

        private void ClearCurrentSyncCursor()
        {
            if (Source != null)
            {
                var connection = Source.GetConnection();
                Source = null;
                if (connection == null)
                    return;

                _ = connection.SendMessage(new SyncCursorReset
                {
                    Cursors = new List<SyncCursor> {
                            new SyncCursor { Type = XdrSyncCursorType.Quanta, DisableSync = true },
                            new SyncCursor { Type = XdrSyncCursorType.MajoritySignatures, DisableSync = true },
                        }
                }.CreateEnvelope<MessageEnvelopeSignless>());
            }
        }

        private void SetCurrentSyncCursor(RemoteNode node)
        {
            Source = node;
            var connection = Source.GetConnection();
            if (connection == null)
                throw new Exception("Unable to find sync source connection");

            var cursors = new List<SyncCursor>
                {
                    new SyncCursor {
                        Type = XdrSyncCursorType.Quanta,
                        Cursor = Context.QuantumHandler.CurrentApex
                    }
                };

            if (IsMajoritySignaturesCursorRequired())
                cursors.Add(new SyncCursor
                {
                    Type = XdrSyncCursorType.MajoritySignatures,
                    Cursor = Context.PendingUpdatesManager.LastPersistedApex
                });

            _ = connection.SendMessage(new SyncCursorReset
            {
                Cursors = cursors
            }.CreateEnvelope<MessageEnvelopeSignless>());
        }

        private bool IsMajoritySignaturesCursorRequired()
        {
            //if the current node is not a prime node, we need to wait for a majority signatures
            //or if the constellation is not ready
            return !(Context.NodesManager.CurrentNode.IsPrimeNode && Context.NodesManager.IsMajorityReady);
        }
    }
}