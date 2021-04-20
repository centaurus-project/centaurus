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


    public class StellarDataProvider : StellarDataProviderBase
    {
        public StellarDataProvider(string networkPassphrase, string horizon)
            : base(networkPassphrase, horizon)
        {
            Network = new Network(networkPassphrase);

            Network.Use(Network);

            Server = new Server(Horizon);
        }

        public Server Server { get; }

        public Network Network { get; }

        public override async Task<AccountModel> GetAccountData(string pubKey)
        {
            var result = await Server.Accounts.Account(pubKey);
            return result
                .ToModel();
        }

        public override async Task<TxModel> GetTransaction(string transactionId)
        {
            var result = await Server.GetTransaction(transactionId);
            return result
                .ToModel();
        }

        public override async Task<List<TxModel>> GetTransactions(string pubKey, long cursor, int limit = 200, bool includeFailed = true)
        {
            var result = await Server
                .GetTransactionsRequestBuilder(pubKey, cursor, limit, includeFailed)
                .Execute();

            return result
                .Records
                .Select(tx => tx.ToModel())
                .ToList();
        }

        public override StellarTxListenerBase GetTransactionListener(string pubKey, long cursor, Action<TxModel> onTxCallback, bool includeFailed = true)
        {
            return new TxListener(Server, pubKey, cursor, includeFailed, onTxCallback);
        }

        public override async Task<TxSubmitModel> SubmitTransaction(Transaction transaction)
        {
            var result = await Server.SubmitTransaction(transaction);
            return result.ToModel();
        }
    }


    public class TxListener : StellarTxListenerBase
    {
        public TxListener(Server server, string pubKey, long cursor, bool includeFailed, Action<TxModel> onTxCallback)
            :base(onTxCallback)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));

            listener = server
                .GetTransactionsRequestBuilder(pubKey, cursor, includeFailed: includeFailed)
                .Stream((_, tx) => onTxCallback(tx.ToModel()));
        }

        public override async Task Connect()
        {
            await listener.Connect();
        }

        public override void Shutdown()
        {
            listener.Shutdown();
        }

        public override void Dispose()
        {
            listener.Dispose();
        }

        private readonly IEventSource listener;
    }
}
