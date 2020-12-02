using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.DAL.Models;

namespace Centaurus.DAL
{
    public interface IStorage: IAnalyticsStorage
    {
        public Task OpenConnection(string connectionString);

        public Task CloseConnection();

        /// <summary>
        /// Fetches last apex presented in DB. Returns -1 if no apex in DB.
        /// </summary>
        public Task<long> GetLastApex();

        /// <summary>
        /// Loads quantum with specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public Task<QuantumModel> LoadQuantum(long apex);

        //TODO: return cursor
        /// <summary>
        /// Loads quanta with specified apexes
        /// </summary>
        /// <param name="apexes"></param>
        /// <returns></returns>
        public Task<List<QuantumModel>> LoadQuanta(params long[] apexes);

        //TODO: return cursor
        /// <summary>
        /// Loads quanta where apex is greater than the specified one
        /// </summary>
        /// <param name="apex"></param>
        /// <param name="count">Count of quanta to load. Loads all if equal or less than 0</param>
        /// <returns></returns>
        public Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0);

        /// <summary>
        /// Returns first effect apex. If it's -1 than there is no effects at all.
        /// </summary>
        /// <returns></returns>
        public Task<long> GetFirstEffectApex();

        public Task<List<EffectModel>> LoadEffectsForApex(long apex);

        public Task<List<EffectModel>> LoadEffectsAboveApex(long apex);

        /// <summary>
        /// Fetches effects
        /// </summary>
        /// <param name="cursor">Effect id.</param>
        /// <param name="isDesc">Is reverse ordering.</param>
        /// <param name="limit">Item per request.</param>
        /// <returns></returns>
        public Task<List<EffectModel>> LoadEffects(byte[] cursor, bool isDesc, int limit, byte[] account);

        public Task<List<AccountModel>> LoadAccounts();

        public Task<List<BalanceModel>> LoadBalances();

        /// <summary>
        /// Fetches settings where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public Task<SettingsModel> LoadSettings(long apex);

        /// <summary>
        /// Fetches assets where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public Task<List<AssetModel>> LoadAssets(long apex);

        public Task<List<QuantumModel>> LoadWithdrawals();

        public Task<List<OrderModel>> LoadOrders();

        public Task<ConstellationState> LoadConstellationState();

        public Task Update(DiffObject update);
    }
}
