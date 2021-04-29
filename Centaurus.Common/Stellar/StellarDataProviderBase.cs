using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.responses.page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Centaurus.Stellar
{
    public abstract class StellarDataProviderBase
    {
        public string NetworkPassphrase { get; }
        public string Horizon { get; }

        public StellarDataProviderBase(string networkPassphrase, string horizon)
        {
            NetworkPassphrase = networkPassphrase ?? throw new ArgumentNullException(nameof(networkPassphrase));
            Horizon = horizon ?? throw new ArgumentNullException(nameof(horizon));
        }

        public abstract Task<TxSubmitModel> SubmitTransaction(stellar_dotnet_sdk.Transaction transaction);
        public abstract Task<TxModel> GetTransaction(string transactionId);
        public abstract Task<List<TxModel>> GetTransactions(string pubKey, long cursor, int limit = 200, bool includeFailed = true);
        public abstract StellarTxListenerBase GetTransactionListener(string pubKey, long cursor, Action<TxModel> onTxCallback, bool includeFailed = true);
        public abstract Task<AccountModel> GetAccountData(string pubKey);
    }

    public abstract class StellarTxListenerBase: IDisposable
    {
        protected readonly Action<TxModel> onTxCallback;

        public StellarTxListenerBase(Action<TxModel> onTxCallback)
        {
            this.onTxCallback = onTxCallback ?? throw new ArgumentNullException(nameof(onTxCallback));
        }

        public abstract Task Connect();

        public abstract void Shutdown();

        public abstract void Dispose();
    }
}
