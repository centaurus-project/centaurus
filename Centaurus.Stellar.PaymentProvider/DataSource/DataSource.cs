using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Stellar
{
    public class DataSource 
    {
        public DataSource(string networkPassphrase, string horizon)
        {
            if (string.IsNullOrWhiteSpace(networkPassphrase))
                throw new ArgumentNullException(nameof(networkPassphrase));

            if (string.IsNullOrWhiteSpace(horizon))
                throw new ArgumentNullException(nameof(horizon));

            Network = new Network(networkPassphrase);

            Network.Use(Network);

            Server = new Server(horizon);
        }

        public Server Server { get; }

        public Network Network { get; }

        public async Task<AccountModel> GetAccountData(string pubKey)
        {
            var result = await Server.Accounts.Account(pubKey);
            return result
                .ToModel();
        }

        public async Task<TxModel> GetTransaction(string transactionId)
        {
            var result = await Server.GetTransaction(transactionId);
            return result
                .ToModel();
        }

        public async Task<List<TxModel>> GetTransactions(string pubKey, long cursor = 0, int limit = 200, bool includeFailed = true, bool isDesc = false)
        {
            var result = await Server
                .GetTransactionsRequestBuilder(pubKey, cursor, limit, includeFailed, isDesc)
                .Execute();

            return result
                .Records
                .Select(tx => tx.ToModel())
                .ToList();
        }

        public TxListener GetTransactionListener(string pubKey, long cursor, Action<TxModel> onTxCallback, bool includeFailed = true)
        {
            return new TxListener(Server, pubKey, cursor, includeFailed, onTxCallback);
        }

        public async Task<TxSubmitModel> SubmitTransaction(Transaction transaction)
        {
            var result = await Server.SubmitTransaction(transaction);
            return result.ToModel();
        }
    }


    public class TxListener
    {
        public TxListener(Server server, string pubKey, long cursor, bool includeFailed, Action<TxModel> onTxCallback)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));

            listener = server
                .GetTransactionsRequestBuilder(pubKey, cursor, includeFailed: includeFailed)
                .Stream((_, tx) => onTxCallback(tx.ToModel()));
        }

        public async Task Connect()
        {
            await listener.Connect();
        }

        public void Shutdown()
        {
            listener.Shutdown();
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        private readonly IEventSource listener;
    }
}
