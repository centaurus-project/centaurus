using Centaurus.Models;
using Centaurus.DAL.Models;
using Centaurus.DAL.Models.Analytics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Threading;

namespace Centaurus.DAL.Mongo
{
    public class MongoStorage : IStorage
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private MongoClient client;
        private IMongoDatabase database;
        private IMongoCollection<OrderModel> ordersCollection;
        private IMongoCollection<AccountModel> accountsCollection;
        private IMongoCollection<BalanceModel> balancesCollection;
        private IMongoCollection<QuantumModel> quantaCollection;
        private IMongoCollection<PaymentCursorModel> paymentCursorsCollection;
        private IMongoCollection<SettingsModel> settingsCollection;

        private IMongoCollection<PriceHistoryFrameModel> priceHistoryCollection;

        private async Task<IMongoCollection<T>> GetCollection<T>(string collectionName)
        {
            if (!(await database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = Builders<BsonDocument>.Filter.Eq("name", collectionName) })).Any())
                await database.CreateCollectionAsync(collectionName);
            return database.GetCollection<T>(collectionName);
        }

        private async Task CreateIndexes()
        {
            await quantaCollection.Indexes.CreateOneAsync(
                 new CreateIndexModel<QuantumModel>(Builders<QuantumModel>.IndexKeys.Ascending(a => a.Accounts).Ascending(a => a.Apex),
                 new CreateIndexOptions { Background = true })
            );
        }

        public async Task OpenConnection(string connectionString)
        {
            var mongoUrl = new MongoUrl(connectionString);

            var conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
            ConventionRegistry.Register("IgnoreExtraElements", conventionPack, type => true);

            client = new MongoClient(mongoUrl);
            database = client.GetDatabase(mongoUrl.DatabaseName);

            ordersCollection = await GetCollection<OrderModel>("orders");
            accountsCollection = await GetCollection<AccountModel>("accounts");
            balancesCollection = await GetCollection<BalanceModel>("balances");
            quantaCollection = await GetCollection<QuantumModel>("quanta");

            paymentCursorsCollection = await GetCollection<PaymentCursorModel>("paymentCursors");

            settingsCollection = await GetCollection<SettingsModel>("constellationSettings");

            priceHistoryCollection = await GetCollection<PriceHistoryFrameModel>("priceHistory");

            await CreateIndexes();
        }

        public Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public async Task<List<OrderModel>> LoadOrders()
        {
            return await ordersCollection
                .Find(FilterDefinition<OrderModel>.Empty)
                .SortBy(o => o.Id)
                .ToListAsync();
        }

        public async Task<SettingsModel> LoadSettings(long apex)
        {
            return await settingsCollection
                .Find(s => s.Apex <= apex)
                .SortByDescending(s => s.Apex)
                .FirstOrDefaultAsync();
        }

        public async Task<List<AccountModel>> LoadAccounts()
        {
            return await accountsCollection
                .Find(FilterDefinition<AccountModel>.Empty)
                .ToListAsync();
        }

        public async Task<List<BalanceModel>> LoadBalances()
        {
            return await balancesCollection
                .Find(FilterDefinition<BalanceModel>.Empty)
                .SortBy(b => b.Id)
                .ToListAsync();
        }

        public async Task<List<PaymentCursorModel>> LoadCursors()
        {
            return await paymentCursorsCollection
                .Find(FilterDefinition<PaymentCursorModel>.Empty)
                .ToListAsync();
        }

        public async Task<QuantumModel> LoadQuantum(long apex)
        {
            return await quantaCollection
                   .Find(q => q.Apex == apex)
                   .FirstAsync();
        }

        public async Task<List<QuantumModel>> LoadQuanta(params long[] apexes)
        {
            var filter = FilterDefinition<QuantumModel>.Empty;
            if (apexes.Length > 0)
                filter = Builders<QuantumModel>.Filter.In(q => q.Apex, apexes);

            var res = await quantaCollection
                .Find(filter)
                .SortBy(q => q.Apex)
                .ToListAsync();

            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");

            return res;
        }

        public async Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0)
        {
            var query = quantaCollection
                .Find(q => q.Apex > apex);
            if (count > 0)
                query = query.Limit(count);
            return await query
                .SortBy(q => q.Apex)
                .ToListAsync();
        }

        public async Task<long> GetFirstApex()
        {
            return (await GetFirstQuantum())?.Apex ?? -1;
        }

        private async Task<QuantumModel> GetFirstQuantum()
        {
            return await quantaCollection
                   .Find(FilterDefinition<QuantumModel>.Empty)
                   .SortBy(e => e.Apex)
                   .FirstOrDefaultAsync();
        }

        public async Task<long> GetLastApex()
        {
            return (await GetLastQuantum())?.Apex ?? -1;
        }

        private async Task<QuantumModel> GetLastQuantum()
        {
            return await quantaCollection
                   .Find(FilterDefinition<QuantumModel>.Empty)
                   .SortByDescending(e => e.Apex)
                   .FirstOrDefaultAsync();
        }

        public async Task<List<QuantumModel>> LoadEffects(long apex, bool isDesc, int limit, int account)
        {
            if (account == default)
                throw new ArgumentNullException(nameof(account));
            var fBuilder = Builders<QuantumModel>.Filter;
            var filter = fBuilder.AnyEq(e => e.Accounts, account);

            if (apex > 0)
            {
                if (isDesc)
                    filter = fBuilder.And(filter, fBuilder.Lt(e => e.Apex, apex));
                else
                    filter = fBuilder.And(filter, fBuilder.Gt(e => e.Apex, apex));
            }
            else
            {
                filter = fBuilder.And(filter, fBuilder.Ne(e => e.Apex, apex));
            }

            var query = quantaCollection
                    .Find(filter);

            if (isDesc)
                query = query
                    .SortByDescending(e => e.Apex);
            else
                query = query
                    .SortBy(e => e.Apex);

            var effects = await query
                .Limit(limit)
                .ToListAsync();

            return effects;
        }


        public async Task<List<PriceHistoryFrameModel>> GetPriceHistory(int cursorTimeStamp, int toUnixTimeStamp, int asset, PriceHistoryPeriod period)
        {
            var cursorId = PriceHistoryExtensions.EncodeId(asset, (int)period, cursorTimeStamp);
            var toId = PriceHistoryExtensions.EncodeId(asset, (int)period, toUnixTimeStamp);
            var query = priceHistoryCollection.Find(
                   Builders<PriceHistoryFrameModel>.Filter.And(
                       Builders<PriceHistoryFrameModel>.Filter.Gte(f => f.Id, cursorId),
                       Builders<PriceHistoryFrameModel>.Filter.Lt(f => f.Id, toId)
                       )
                   ).SortByDescending(x => x.Id);
            return await query
                .ToListAsync();
        }

        public async Task<int> GetFirstPriceHistoryFrameDate(int market, PriceHistoryPeriod period)
        {
            var firstId = PriceHistoryExtensions.EncodeId(market, (int)period, 0);

            var firstFrame = await priceHistoryCollection
                .Find(Builders<PriceHistoryFrameModel>.Filter.Gte(f => f.Id, firstId))
                .SortBy(e => e.Id)
                .FirstOrDefaultAsync();

            if (firstFrame == null)
                return 0;

            return PriceHistoryExtensions.DecodeId(firstFrame.Id).timestamp;
        }

        #region Updates


        static TransactionOptions txOptions = new TransactionOptions(ReadConcern.Local, writeConcern: WriteConcern.W1.With(journal: false));
        public async Task<int> Update(DiffObject update)
        {
            var constellationUpdate = GetConstellationUpdate(update.Cursors.Values.ToList());
            var accountUpdates = GetAccountUpdates(update.Accounts.Values.ToList());
            var balanceUpdates = GetBalanceUpdates(update.Balances.Values.ToList());
            var orderUpdates = GetOrderUpdates(update.Orders.Values.ToList());
            var quanta = GetQuantaUpdates(update.Quanta);
            
            using (var session = await client.StartSessionAsync())
            {
                var result = await session.WithTransactionAsync<bool>(async (s, ct) =>
                {
                    var updateTasks = new List<Task>();

                    if (constellationUpdate != null)
                        updateTasks.Add(paymentCursorsCollection.BulkWriteAsync(s, constellationUpdate, cancellationToken: ct));

                    if (update.ConstellationSettings != null)
                        updateTasks.Add(settingsCollection.InsertOneAsync(s, update.ConstellationSettings, cancellationToken: ct));

                    if (accountUpdates != null)
                        updateTasks.AddRange(accountsCollection.WriteBatch(s, accountUpdates, ct));

                    if (balanceUpdates != null)
                        updateTasks.AddRange(balancesCollection.WriteBatch(s, balanceUpdates, ct));

                    if (orderUpdates != null)
                        updateTasks.AddRange(ordersCollection.WriteBatch(s, orderUpdates, ct));

                    updateTasks.AddRange(quantaCollection.WriteBatch(s, quanta, ct));

                    await Task.WhenAll(updateTasks);

                    return true;
                },
                txOptions,
                CancellationToken.None);
                return 1;
            }
        }

        private List<WriteModel<PaymentCursorModel>> GetConstellationUpdate(List<DiffObject.PaymentCursor> cursors)
        {
            if (cursors == null || cursors.Count < 1)
                return null;

            var cursorUpdates = new List<WriteModel<PaymentCursorModel>>(cursors.Count);
            foreach (var cursor in cursors)
            {
                WriteModel<PaymentCursorModel> updateModel = null;
                if (cursor.IsInserted)
                    updateModel = new InsertOneModel<PaymentCursorModel>(new PaymentCursorModel
                    {
                        Provider = cursor.Provider,
                        Cursor = cursor.Cursor
                    });
                else if (cursor.IsDeleted)
                    throw new InvalidOperationException($"{cursor.Provider} cursor cannot be deleted.");
                else
                {
                    updateModel = new UpdateOneModel<PaymentCursorModel>(
                        Builders<PaymentCursorModel>.Filter.Eq(c => c.Provider, cursor.Provider), 
                        Builders<PaymentCursorModel>.Update.Set(s => s.Cursor, cursor.Cursor)
                    );
                }
            }
            return cursorUpdates;
        }

        private List<WriteModel<AccountModel>> GetAccountUpdates(List<DiffObject.Account> accounts)
        {
            if (accounts == null || accounts.Count < 1)
                return null;
            var filter = Builders<AccountModel>.Filter;
            var update = Builders<AccountModel>.Update;

            var accLength = accounts.Count;
            var updates = new List<WriteModel<AccountModel>>(accLength);
            updates.AddRange(Enumerable.Repeat(default(WriteModel<AccountModel>), accLength));

            Parallel.For(0, accLength, (i) =>
            {
                var acc = accounts[i];
                var currentAccFilter = filter.Eq(a => a.PubKey, acc.PubKey);
                if (acc.IsInserted)
                    updates[i] = new InsertOneModel<AccountModel>(new AccountModel
                    {
                        Nonce = acc.Nonce,
                        PubKey = acc.PubKey,
                        RequestRateLimits = acc.RequestRateLimits
                    });
                else if (acc.IsDeleted)
                    updates[i] = new DeleteOneModel<AccountModel>(currentAccFilter);
                else
                {
                    var updateDefs = new List<UpdateDefinition<AccountModel>>();
                    if (acc.Nonce != 0)
                        updateDefs.Add(update.Set(a => a.Nonce, acc.Nonce));

                    if (acc.RequestRateLimits != null)
                        updateDefs.Add(update.Set(a => a.RequestRateLimits, acc.RequestRateLimits));

                    updates[i] = new UpdateOneModel<AccountModel>(currentAccFilter, update.Combine(updateDefs));
                }
            });
            return updates;
        }

        private List<WriteModel<BalanceModel>> GetBalanceUpdates(List<DiffObject.Balance> balances)
        {
            if (balances == null || balances.Count < 1)
                return null;
            var filter = Builders<BalanceModel>.Filter;
            var update = Builders<BalanceModel>.Update;

            var balancesLength = balances.Count;
            var updates = new List<WriteModel<BalanceModel>>(balancesLength);
            updates.AddRange(Enumerable.Repeat(default(WriteModel<BalanceModel>), balancesLength));

            Parallel.For(0, balancesLength, (i) =>
            {
                var balance = balances[i];
                var currentBalanceFilter = filter.Eq(s => s.Id, balance.Id);

                if (balance.IsInserted)
                    updates[i] = new InsertOneModel<BalanceModel>(new BalanceModel
                    {
                        Id = balance.Id,
                        Amount = balance.AmountDiff,
                        Liabilities = balance.LiabilitiesDiff
                    });
                else if (balance.IsDeleted)
                    updates[i] = new DeleteOneModel<BalanceModel>(currentBalanceFilter);
                else
                {
                    updates[i] = new UpdateOneModel<BalanceModel>(
                        currentBalanceFilter,
                        update
                            .Inc(b => b.Amount, balance.AmountDiff)
                            .Inc(b => b.Liabilities, balance.LiabilitiesDiff)
                    );
                }
            });
            return updates;
        }

        private List<WriteModel<OrderModel>> GetOrderUpdates(List<DiffObject.Order> orders)
        {
            if (orders == null || orders.Count < 1)
                return null;
            unchecked
            {
                var filter = Builders<OrderModel>.Filter;
                var update = Builders<OrderModel>.Update;

                var ordersLength = orders.Count;
                var updates = new List<WriteModel<OrderModel>>(ordersLength);
                updates.AddRange(Enumerable.Repeat(default(WriteModel<OrderModel>), ordersLength));

                Parallel.For(0, ordersLength, (i) =>
                {
                    var order = orders[i];

                    var currentOrderFilter = filter.And(filter.Eq(s => s.Id, (long) order.OrderId));

                    if (order.IsInserted)
                        updates[i] = new InsertOneModel<OrderModel>(new OrderModel
                        {
                            Id = (long) order.OrderId,
                            Amount = order.AmountDiff,
                            QuoteAmount = order.QuoteAmountDiff,
                            Price = order.Price,
                            Account = order.Account
                        });
                    else if (order.IsDeleted)
                        updates[i] = new DeleteOneModel<OrderModel>(currentOrderFilter);
                    else
                    {
                        updates[i] = new UpdateOneModel<OrderModel>(
                            currentOrderFilter,
                            update.Inc(b => b.Amount, order.AmountDiff).Inc(b => b.QuoteAmount, order.QuoteAmountDiff)
                        );
                    }
                });
                return updates;
            }
        }

        private List<WriteModel<QuantumModel>> GetQuantaUpdates(List<QuantumModel> quanta)
        {
            return quanta.Select(q => (WriteModel<QuantumModel>)new InsertOneModel<QuantumModel>(q)).ToList();
        }

        public async Task SaveAnalytics(List<PriceHistoryFrameModel> frames)
        {
            await priceHistoryCollection.BulkWriteAsync(PrepareFramesUpdateBatch(frames));
        }

        private List<ReplaceOneModel<PriceHistoryFrameModel>> PrepareFramesUpdateBatch(List<PriceHistoryFrameModel> frames)
        {
            var updates = new List<ReplaceOneModel<PriceHistoryFrameModel>>();
            var filter = Builders<PriceHistoryFrameModel>.Filter;
            foreach (var frame in frames)
                updates.Add(new ReplaceOneModel<PriceHistoryFrameModel>(filter.Eq(f => f.Id, frame.Id), frame) { IsUpsert = true });
            return updates;
        }

        public async Task DropDatabase()
        {
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }

        #endregion
    }
}
