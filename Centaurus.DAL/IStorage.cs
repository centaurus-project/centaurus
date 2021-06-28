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
        /// Fetches first apex presented in DB. Returns -1 if no apex in DB.
        /// </summary>
        public Task<long> GetFirstApex();

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

        /// <summary>
        /// Loads quanta with specified apexes
        /// </summary>
        /// <param name="apexes"></param>
        /// <returns></returns>
        public Task<List<QuantumModel>> LoadQuanta(params long[] apexes);

        /// <summary>
        /// Loads quanta where apex is greater than the specified one
        /// </summary>
        /// <param name="apex"></param>
        /// <param name="count">Count of quanta to load. Loads all if equal or less than 0</param>
        /// <returns></returns>
        public Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0);

        /// <summary>
        /// Fetches effects
        /// </summary>
        /// <param name="cursor">Effects apex.</param>
        /// <param name="isDesc">Is reverse ordering.</param>
        /// <param name="limit">Item per request.</param>
        /// <returns></returns>
        public Task<List<QuantumModel>> LoadEffects(long apex, bool isDesc, int limit, int account);

        public Task<List<AccountModel>> LoadAccounts();

        public Task<List<BalanceModel>> LoadBalances();

        /// <summary>
        /// Fetches settings where apex is equal to or lower than specified one. 
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public Task<SettingsModel> LoadSettings(long apex);

        //returns sorted by id orders
        public Task<List<OrderModel>> LoadOrders();

        public Task<List<PaymentCursorModel>> LoadCursors();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="update"></param>
        /// <returns>Retries count. (MongoDb often throws transaction exception and update command must be repeated)</returns>
        public Task<int> Update(DiffObject update);

        public Task DropDatabase();
    }
}
