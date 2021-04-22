using Centaurus.Stellar;
using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.responses.page;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockStellarDataProvider : StellarDataProviderBase
    {
        public MockStellarDataProvider(string networkPassphrase, string horizon)
            : base(networkPassphrase, horizon)
        {
            var network = new Network(networkPassphrase);
            Network.Use(network);
        }

        public event Action<TxModel> TxAdded;

        private Dictionary<string, AccountModel> accounts = new Dictionary<string, AccountModel>();

        public void RegisterAccount(AccountModel accountModel)
        {
            accounts.Add(accountModel.KeyPair.AccountId, accountModel);
        }

        public override Task<AccountModel> GetAccountData(string pubKey)
        {
            accounts.TryGetValue(pubKey, out var data);
            return Task.FromResult(data);
        }

        public Dictionary<string, TxModel> Txs = new Dictionary<string, TxModel>();
        public override Task<TxModel> GetTransaction(string transactionId)
        {
            Txs.TryGetValue(transactionId, out var data);
            return Task.FromResult(data);
        }

        public override Task<List<TxModel>> GetTransactions(string pubKey, long cursor, int limit = 200, bool includeFailed = true)
        {
            return Task.FromResult(GetTransactionsInternal(pubKey, cursor, limit, includeFailed));
        }

        private List<TxModel> GetTransactionsInternal(string pubKey, long cursor, int limit = 200, bool includeFailed = true)
        {
            lock (syncRoot)
            {
                var q = Txs
                    .Values
                    .Where(t => t.PagingToken > cursor);
                if (!includeFailed)
                    q = q.Where(t => t.IsSuccess);
                return q
                     .OrderBy(t => t.PagingToken)
                     .Take(limit)
                     .ToList();
            }
        }

        private object syncRoot = new { };

        public override StellarTxListenerBase GetTransactionListener(string pubKey, long cursor, Action<TxModel> onTxCallback, bool includeFailed = true)
        {
            lock (syncRoot)
            {
                var txs = GetTransactionsInternal(pubKey, cursor, int.MaxValue, includeFailed);
                return new MockStellarTxListenerBase(this, txs, onTxCallback);
            }
        }

        public override Task<TxSubmitModel> SubmitTransaction(Transaction transaction)
        {
            return SubmitTx(transaction, true);
        }

        public Task SubmitFailedTransaction(Transaction transaction)
        {
            return SubmitTx(transaction, false);
        }

        private Task<TxSubmitModel> SubmitTx(Transaction transaction, bool isSuccess)
        {
            var txModel = GetTxModel(transaction, isSuccess);
            lock (syncRoot)
            {
                Txs.Add(txModel.Hash, txModel);
                TxAdded?.Invoke(txModel);
            }
            var submitTxResult = new TxSubmitModel
            {
                Hash = txModel.Hash,
                IsSuccess = txModel.IsSuccess
            };
            return Task.FromResult(submitTxResult);
        }

        private TxModel GetTxModel(Transaction tx, bool isSuccess = true)
        {
            var envelope = default(stellar_dotnet_sdk.xdr.TransactionEnvelope);
            if (tx.Signatures.Count > 0)
                envelope = tx.ToEnvelopeXdr();
            else
                envelope = tx.ToUnsignedEnvelopeXdr();

            var writer = new stellar_dotnet_sdk.xdr.XdrDataOutputStream();
            stellar_dotnet_sdk.xdr.TransactionV1Envelope.Encode(writer, envelope.V1);
            var hash = tx.Hash().ToHex().ToLower();

            return new TxModel
            {
                PagingToken = (Txs.Values.LastOrDefault()?.PagingToken ?? 0) + 1,
                EnvelopeXdr = Convert.ToBase64String(writer.ToArray()),
                Hash = hash,
                IsSuccess = isSuccess
            };
        }

        public class MockStellarTxListenerBase : StellarTxListenerBase
        {
            private MockStellarDataProvider dataProvider;
            private BlockingCollection<TxModel> txs = new BlockingCollection<TxModel>();
            private CancellationTokenSource cts = new CancellationTokenSource();

            public MockStellarTxListenerBase(MockStellarDataProvider dataProvider, List<TxModel> txs, Action<TxModel> onTxCallback)
                :base(onTxCallback)
            {
                this.dataProvider = dataProvider;
                dataProvider.TxAdded += DataProvider_TxAdded;
                foreach (var tx in txs)
                    this.txs.Add(tx);
            }

            public override Task Connect()
            {
                Task.Factory.StartNew(StartSendTxs);
                return Task.CompletedTask;
            }

            private void StartSendTxs()
            {
                foreach (var tx in txs.GetConsumingEnumerable(cts.Token))
                    onTxCallback.Invoke(tx);
            }

            private void DataProvider_TxAdded(TxModel obj)
            {
                txs.Add(obj);
            }

            public override void Dispose()
            {
                dataProvider.TxAdded -= DataProvider_TxAdded;
                txs.Dispose();
                cts.Dispose();
            }

            public override void Shutdown()
            {
                cts.Cancel();
            }
        }
    }
}
