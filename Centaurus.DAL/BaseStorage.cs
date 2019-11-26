using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.DAL.Models;

namespace Centaurus.DAL
{
    public abstract class BaseStorage
    {
        public abstract Task OpenConnection(string connectionString);

        public abstract Task CloseConnection();

        public abstract Task<ulong> GetLastApex();

        //TODO: return cursor
        public abstract Task<List<QuantumModel>> LoadQuanta(params ulong[] ids);

        public abstract Task<List<EffectModel>> LoadEffectsForApex(ulong apex);

        public abstract Task<List<EffectModel>> LoadEffectsAboveApex(ulong apex);

        public abstract Task<List<AccountModel>> LoadAccounts();

        public abstract Task<List<BalanceModel>> LoadBalances();

        /// <summary>
        /// Fetches settings where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public abstract Task<SettingsModel> LoadSettings(ulong apex);

        /// <summary>
        /// Fetches assets where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public abstract Task<List<AssetSettingsModel>> LoadAssets(ulong apex);

        public abstract Task<List<WithdrawalModel>> LoadWithdrawals();

        public abstract Task<List<OrderModel>> LoadOrders();

        public abstract Task<StellarData> LoadStellarData();

        public abstract Task Update(UpdateObject update);
    }
}
