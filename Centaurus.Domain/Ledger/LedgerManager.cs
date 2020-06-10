using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class LedgerManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public LedgerManager(long ledger)
        {
            Ledger = ledger;

            //only auditors listen to the ledger
            if (!Global.IsAlpha && !EnvironmentHelper.IsTest)
            {
                var ledgerCursor = (Global.StellarNetwork.Server.Ledgers.Ledger(Ledger).Result).PagingToken;

                EnsureLedgerListenerIsDisposed();
                _ = ListenLedger(ledgerCursor);

                lastSentLedgerSequence = currentLedgerSequence = (uint)ledger;
            }
        }

        private object syncRoot = new { };

        public void SetLedger(long ledger)
        {
            lock (syncRoot)
                Ledger = ledger;
        }

        public bool IsValidNextLedger(long nextLedger)
        {
            lock (syncRoot)
                return Ledger + 1 == nextLedger;
        }

        public long Ledger { get; private set; }

        private void EnsureLedgerListenerIsDisposed()
        {
            listener?.Shutdown();
            listener?.Dispose();
        }

        private IEventSource listener;
        private uint lastSentLedgerSequence;
        private uint currentLedgerSequence;

        private async Task ListenLedger(string ledgerCursor)
        {
            listener = Global.StellarNetwork.Server.Ledgers
                .Cursor(ledgerCursor)
                .Stream(ProcessLedgerPayments);

            await listener.Connect();
        }

        private void AddVaultPayments(ref List<PaymentBase> ledgerPayments, Transaction transaction, bool isSuccess)
        {
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var source = transaction.SourceAccount;
            //TODO: add only success or if transaction hash is in pending withdrawals
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                if (PaymentsHelper.FromOperationResponse(transaction.Operations[i].ToOperationBody(), source, res, transaction.Hash(), out PaymentBase payment))
                    ledgerPayments.Add(payment);
            }
        }
        private void ProcessLedgerPayments(object sender, LedgerResponse ledgerResponse)
        {
            try
            {
                if (ledgerResponse.Sequence == currentLedgerSequence)
                {
                    logger.Trace("Already handled ledger arrived");
                    return;
                }
                else if (ledgerResponse.Sequence != currentLedgerSequence + 1)
                {
                    throw new Exception($"The ledger received from the horizon is not sequential. {currentLedgerSequence + 1} was expected, but got {ledgerResponse.Sequence}");
                }

                var pagingToken = (ledgerResponse.Sequence << 32).ToString();

                //TODO: try several time to load resources before throw exception
                var result = Global.StellarNetwork.Server.Transactions
                    .ForAccount(Global.Constellation.Vault.ToString())
                    .Cursor(pagingToken)
                    .Limit(200)
                    .Execute().Result;

                var payments = new List<PaymentBase>();
                while (result.Records.Count > 0)
                {
                    var transactions = result.Records
                        .Where(t => t.Ledger == ledgerResponse.Sequence);

                    //transactions is out of ledger
                    if (transactions.Count() < 1)
                        break;
                    foreach (var transaction in transactions)
                        AddVaultPayments(ref payments, Transaction.FromEnvelopeXdr(transaction.EnvelopeXdr), transaction.Result.IsSuccess);

                    result = result.NextPage().Result;
                }

                currentLedgerSequence++;

                //if there are any payments, or ledger sequence is multiple of 64
                if (payments.Count > 0 || (((uint)ledgerResponse.Sequence + 1) % 64 == 0))
                {
                    var ledger = new LedgerUpdateNotification
                    {
                        LedgerFrom = lastSentLedgerSequence + 1,
                        LedgerTo = (uint)ledgerResponse.Sequence,
                        Payments = payments
                    };

                    logger.Trace($"Ledger range from {ledger.LedgerFrom} to {ledger.LedgerTo} is handled. Number of payments for account {Global.Constellation.Vault.ToString()} is {ledger.Payments.Count}.");

                    lastSentLedgerSequence = (uint)ledgerResponse.Sequence;

                    OutgoingMessageStorage.OnLedger(ledger);
                }
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e);

                //if ledger worker is broken, the auditor should quit consensus
                Global.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();

                throw;
            }
        }

        public void Dispose()
        {
            listener?.Shutdown();
            listener?.Dispose();
        }
    }
}
