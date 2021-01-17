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

namespace Centaurus.DAL.Mongo
{
    public class MongoStorage : IStorage
    {
        private MongoClient client;
        private IMongoDatabase database;
        private IMongoCollection<OrderModel> ordersCollection;
        private IMongoCollection<AccountModel> accountsCollection;
        private IMongoCollection<BalanceModel> balancesCollection;
        private IMongoCollection<QuantumModel> quantaCollection;
        private IMongoCollection<ConstellationState> constellationStateCollection;
        private IMongoCollection<EffectModel> effectsCollection;
        private IMongoCollection<SettingsModel> settingsCollection;
        private IMongoCollection<AssetModel> assetsCollection;

        private IMongoCollection<PriceHistoryFrameModel> priceHistoryCollection;

        private async Task CreateIndexes()
        {
            await quantaCollection.Indexes.CreateOneAsync(
                 new CreateIndexModel<QuantumModel>(Builders<QuantumModel>.IndexKeys.Ascending(q => q.TimeStamp).Descending(q => q.Apex),
                 new CreateIndexOptions { Background = true })
            );

            await effectsCollection.Indexes.CreateOneAsync(
                 new CreateIndexModel<EffectModel>(Builders<EffectModel>.IndexKeys.Ascending(e => e.Id).Ascending(e => e.EffectType),
                 new CreateIndexOptions { Background = true })
            );
        }

        private async Task<IMongoCollection<T>> GetCollection<T>(string collectionName)
        {
            if (!(await database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = Builders<BsonDocument>.Filter.Eq("name", collectionName) })).Any())
                await database.CreateCollectionAsync(collectionName);
            return database.GetCollection<T>(collectionName);
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
            constellationStateCollection = await GetCollection<ConstellationState>("constellationState");

            effectsCollection = await GetCollection<EffectModel>("effects");

            settingsCollection = await GetCollection<SettingsModel>("constellationSettings");

            assetsCollection = await GetCollection<AssetModel>("assets");

            priceHistoryCollection = await GetCollection<PriceHistoryFrameModel>("priceHistory");

            await CreateIndexes();
        }

        public Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public async Task<List<QuantumModel>> LoadWithdrawals()
        {
            var lastQuantum = await GetLastQuantum();
            if (lastQuantum == null) //no quanta no withdrawals
                return new List<QuantumModel>();

            var timestampFrom = (new DateTime(lastQuantum.TimeStamp, DateTimeKind.Utc) - TimeSpan.FromMinutes(120)).Ticks;
            var fromQuantum = await quantaCollection
                .Find(q => q.TimeStamp <= timestampFrom)
                .SortByDescending(q => q.Apex)
                .FirstOrDefaultAsync();

            var fromId = EffectModelIdConverter.EncodeId(fromQuantum?.Apex ?? 0, 0);

            var effectTypes = new int[] { (int)EffectTypes.WithdrawalCreate, (int)EffectTypes.WithdrawalRemove };

            var eFilter = Builders<EffectModel>.Filter;

            var withdrawalEffects = await effectsCollection.Aggregate()
                .Match(eFilter.And(eFilter.Gte(e => e.Id, fromId), eFilter.In(e => e.EffectType, effectTypes)))
                .SortByDescending(e => e.Id)
                .Group(new BsonDocument {
                    { "_id", "$Account" },
                    { "effectId", new BsonDocument { { "$first", "$_id" } } },
                    { "lastEffectType", new BsonDocument { { "$first", "$EffectType" } } }
                })
                .Match(new BsonDocument { { "lastEffectType", (int)EffectTypes.WithdrawalCreate } })
                .ToListAsync();

            var quantumIds = withdrawalEffects
                .Select(q =>
                {
                    var effectId = q["effectId"].AsObjectId;
                    var decoded = EffectModelIdConverter.DecodeId(effectId);
                    return decoded.apex;
                });

            var withdrawals = await quantaCollection
                .Find(Builders<QuantumModel>.Filter.In(q => q.Apex, quantumIds))
                .SortBy(q => q.Apex)
                .ToListAsync();
            return withdrawals;
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

        public async Task<List<AssetModel>> LoadAssets(long apex)
        {
            return await assetsCollection
                .Find(a => a.Apex <= apex)
                .SortBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<List<AccountModel>> LoadAccounts()
        {
            return await accountsCollection
                .Find(FilterDefinition<AccountModel>.Empty)
                .SortBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<List<BalanceModel>> LoadBalances()
        {
            return await balancesCollection
                .Find(FilterDefinition<BalanceModel>.Empty)
                .SortBy(b => b.Id)
                .ToListAsync();
        }

        public async Task<ConstellationState> LoadConstellationState()
        {
            return await constellationStateCollection
                .Find(FilterDefinition<ConstellationState>.Empty)
                .FirstOrDefaultAsync();
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

        public async Task<long> GetFirstEffectApex()
        {
            var firstEffect = await effectsCollection
                   .Find(FilterDefinition<EffectModel>.Empty)
                   .SortBy(e => e.Id)
                   .FirstOrDefaultAsync();
            if (firstEffect == null)
                return -1;

            var decodedId = EffectModelIdConverter.DecodeId(firstEffect.Id);
            return decodedId.apex;
        }

        public async Task<List<EffectModel>> LoadEffectsForApex(long apex)
        {
            var fromCursor = EffectModelIdConverter.EncodeId(apex, 0);
            var toCursor = EffectModelIdConverter.EncodeId(apex + 1, 0);
            return await effectsCollection
                .Find(e => e.Id >= fromCursor && e.Id < toCursor)
                .SortBy(e => e.Id)
                .ToListAsync();
        }

        public async Task<List<EffectModel>> LoadEffectsAboveApex(long apex)
        {
            var fromCursor = EffectModelIdConverter.EncodeId(apex + 1, 0);
            return await effectsCollection
                .Find(e => e.Id >= fromCursor)
                .SortBy(e => e.Id)
                .ToListAsync();
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

        public async Task<List<EffectModel>> LoadEffects(byte[] cursor, bool isDesc, int limit, int account)
        {
            if (account == default)
                throw new ArgumentNullException(nameof(account));
            var filter = Builders<EffectModel>.Filter.Eq(e => e.Account, account);

            if (cursor != null && cursor.Any(x => x != 0))
            {
                var c = new BsonObjectId(new ObjectId(cursor));
                if (isDesc)
                    filter = Builders<EffectModel>.Filter.And(filter, Builders<EffectModel>.Filter.Lt(e => e.Id, c));
                else
                    filter = Builders<EffectModel>.Filter.And(filter, Builders<EffectModel>.Filter.Gt(e => e.Id, c));
            }

            var query = effectsCollection
                    .Find(filter);

            if (isDesc)
                query = query
                    .SortByDescending(e => e.Id);
            else
                query = query
                    .SortBy(e => e.Id);

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

        public async Task Update(DiffObject update)
        {
            using (var session = await client.StartSessionAsync())
            {
                session.StartTransaction();

                try
                {
                    var updateTasks = new List<Task>();
                    if (update.ConstellationSettings != null)
                    {
                        updateTasks.Add(settingsCollection.InsertOneAsync(session, update.ConstellationSettings));
                        updateTasks.Add(assetsCollection.InsertManyAsync(session, update.Assets));
                    }
                    if (update.StellarInfoData != null)
                    {
                        var stellarUpdates = GetStellarDataUpdate(update.StellarInfoData);
                        updateTasks.Add(constellationStateCollection.BulkWriteAsync(session, new WriteModel<ConstellationState>[] { stellarUpdates }));
                    }

                    if (update.Accounts != null && update.Accounts.Count > 0)
                    {
                        var accountUpdates = GetAccountUpdates(update.Accounts.Values.ToList());
                        if (accountUpdates.Length < 1)
                            throw new Exception("Unable to get account updates.");
                        updateTasks.Add(accountsCollection.BulkWriteAsync(session, accountUpdates));
                    }

                    if (update.Balances != null && update.Balances.Count > 0)
                    {
                        var balanceUpdates = GetBalanceUpdates(update.Balances.Values.ToList());
                        if (balanceUpdates.Length < 1)
                            throw new Exception("Unable to get balance updates.");
                        updateTasks.Add(balancesCollection.BulkWriteAsync(session, balanceUpdates));
                    }

                    if (update.Orders != null && update.Orders.Count > 0)
                    {
                        var orderUpdates = GetOrderUpdates(update.Orders.Values.ToList());
                        if (orderUpdates.Length < 1)
                            throw new Exception("Unable to get order updates.");
                        updateTasks.Add(ordersCollection.BulkWriteAsync(session, orderUpdates));
                    }

                    if (update.Quanta.Count < 1)
                        throw new Exception("Quanta doesn't contain items.");
                    updateTasks.Add(quantaCollection.InsertManyAsync(session, update.Quanta));

                    if (update.Effects.Count < 1)
                        throw new Exception("Effects doesn't contain items.");
                    updateTasks.Add(effectsCollection.InsertManyAsync(session, update.Effects));

                    await Task.WhenAll(updateTasks);

                    await session.CommitTransactionAsync();
                }
                catch
                {
                    await session.AbortTransactionAsync();
                    throw;
                }
            }
        }

        private WriteModel<ConstellationState> GetStellarDataUpdate(DiffObject.ConstellationState constellationState)
        {
            var cursor = constellationState.TxCursor;

            WriteModel<ConstellationState> updateModel = null;
            if (constellationState.IsInserted)
                updateModel = new InsertOneModel<ConstellationState>(new ConstellationState
                {
                    TxCursor = cursor
                });
            else if (constellationState.IsDeleted)
                throw new InvalidOperationException("Stellar data entry cannot be deleted");
            else
            {
                updateModel = new UpdateOneModel<ConstellationState>(Builders<ConstellationState>.Filter.Empty, Builders<ConstellationState>.Update.Set(s => s.TxCursor, cursor));
            }

            return updateModel;
        }

        private WriteModel<AccountModel>[] GetAccountUpdates(List<DiffObject.Account> accounts)
        {
            unchecked
            {
                var filter = Builders<AccountModel>.Filter;
                var update = Builders<AccountModel>.Update;

                var accLength = accounts.Count;
                var updates = new WriteModel<AccountModel>[accLength];

                for (int i = 0; i < accLength; i++)
                {
                    var acc = accounts[i];
                    var currentAccFilter = filter.Eq(a => a.Id, acc.Id);
                    if (acc.IsInserted)
                        updates[i] = new InsertOneModel<AccountModel>(new AccountModel
                        {
                            Id = acc.Id,
                            Nonce = acc.Nonce,
                            PubKey = acc.PubKey,
                            RequestRateLimits = acc.RequestRateLimits
                        });
                    else if (acc.IsDeleted)
                        updates[i] = new DeleteOneModel<AccountModel>(currentAccFilter);
                    else
                    {
                        UpdateDefinition<AccountModel> currentUpdate = null;
                        if (acc.Nonce != 0)
                            currentUpdate = update.Set(a => a.Nonce, acc.Nonce);
                        if (acc.RequestRateLimits != null)
                            currentUpdate = currentUpdate == null
                                ? update.Set(a => a.RequestRateLimits, acc.RequestRateLimits)
                                : currentUpdate.Set(a => a.RequestRateLimits, acc.RequestRateLimits);
                        updates[i] = new UpdateOneModel<AccountModel>(currentAccFilter, currentUpdate);
                    }
                }
                return updates;
            }
        }

        private WriteModel<BalanceModel>[] GetBalanceUpdates(List<DiffObject.Balance> balances)
        {
            var filter = Builders<BalanceModel>.Filter;
            var update = Builders<BalanceModel>.Update;

            var balancesLength = balances.Count;
            var updates = new WriteModel<BalanceModel>[balancesLength];

            for (int i = 0; i < balancesLength; i++)
            {
                var balance = balances[i];
                var currentBalanceFilter = filter.Eq(s => s.Id, balance.Id);

                if (balance.IsInserted)
                    updates[i] = new InsertOneModel<BalanceModel>(new BalanceModel
                    {
                        Id = balance.Id,
                        Amount = balance.Amount,
                        Liabilities = balance.Liabilities
                    });
                else if (balance.IsDeleted)
                    updates[i] = new DeleteOneModel<BalanceModel>(currentBalanceFilter);
                else
                {
                    updates[i] = new UpdateOneModel<BalanceModel>(
                        currentBalanceFilter,
                        update
                            .Inc(b => b.Amount, balance.Amount)
                            .Inc(b => b.Liabilities, balance.Liabilities)
                        );
                }
            }
            return updates;
        }

        private WriteModel<OrderModel>[] GetOrderUpdates(List<DiffObject.Order> orders)
        {
            unchecked
            {
                var filter = Builders<OrderModel>.Filter;
                var update = Builders<OrderModel>.Update;

                var ordersLength = orders.Count;
                var updates = new WriteModel<OrderModel>[ordersLength];

                for (int i = 0; i < ordersLength; i++)
                {
                    var order = orders[i];

                    var currentOrderFilter = filter.And(filter.Eq(s => s.Id, (long)order.OrderId));

                    if (order.IsInserted)
                        updates[i] = new InsertOneModel<OrderModel>(new OrderModel
                        {
                            Id = (long)order.OrderId,
                            Amount = order.Amount,
                            Price = order.Price,
                            Account = order.Account
                        });
                    else if (order.IsDeleted)
                        updates[i] = new DeleteOneModel<OrderModel>(currentOrderFilter);
                    else
                    {
                        updates[i] = new UpdateOneModel<OrderModel>(
                            currentOrderFilter,
                            update.Inc(b => b.Amount, order.Amount)
                        );
                    }
                }
                return updates;
            }
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
