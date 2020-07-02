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

        /// <summary>
        /// Fetches last apex presented in DB. Returns -1 if no apex in DB.
        /// </summary>
        public abstract Task<long> GetLastApex();

        /// <summary>
        /// Loads quantum with specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public abstract Task<QuantumModel> LoadQuantum(long apex);

        //TODO: return cursor
        /// <summary>
        /// Loads quanta with specified apexes
        /// </summary>
        /// <param name="apexes"></param>
        /// <returns></returns>
        public abstract Task<List<QuantumModel>> LoadQuanta(params long[] apexes);

        //TODO: return cursor
        /// <summary>
        /// Loads quanta where apex is greater than the specified one
        /// </summary>
        /// <param name="apex"></param>
        /// <param name="count">Count of quanta to load. Loads all if equal or less than 0</param>
        /// <returns></returns>
        public abstract Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0);

        /// <summary>
        /// Returns first effect apex. If it's -1 than there is no effects at all.
        /// </summary>
        /// <returns></returns>
        public abstract Task<long> GetFirstEffectApex();

        public abstract Task<List<EffectModel>> LoadEffectsForApex(long apex);

        public abstract Task<List<EffectModel>> LoadEffectsAboveApex(long apex);

        /// <summary>
        /// Fetches effects
        /// </summary>
        /// <param name="effectsPagingToken">Token that contains all info about the request</param>
        /// <param name="account">Account for which effects will be loaded. If it's null then all effects will be loaded.</param>
        /// <returns></returns>
        public abstract Task<CursorResult<EffectModel>> LoadEffects(EffectsPagingToken effectsPagingToken, byte[] account);

        public abstract Task<List<AccountModel>> LoadAccounts();

        public abstract Task<List<BalanceModel>> LoadBalances();

        /// <summary>
        /// Fetches settings where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public abstract Task<SettingsModel> LoadSettings(long apex);

        /// <summary>
        /// Fetches assets where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public abstract Task<List<AssetModel>> LoadAssets(long apex);

        public abstract Task<List<WithdrawalModel>> LoadWithdrawals();

        public abstract Task<List<OrderModel>> LoadOrders();

        public abstract Task<ConstellationState> LoadConstellationState();

        public abstract Task Update(DiffObject update);
    }
}
