using Centaurus.DAL.Models;
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
    public class MongoStorage : BaseStorage
    {
        private MongoClient client;
        private IMongoDatabase database;
        private IMongoCollection<OrderModel> ordersCollection;
        private IMongoCollection<AccountModel> accountsCollection;
        private IMongoCollection<BalanceModel> balancesCollection;
        private IMongoCollection<QuantumModel> quantaCollection;
        private IMongoCollection<ConstellationState> constellationStateCollection;
        private IMongoCollection<WithdrawalModel> withdrawalsCollection;
        private IMongoCollection<EffectModel> effectsCollection;
        private IMongoCollection<SettingsModel> settingsCollection;
        private IMongoCollection<AssetModel> assetsCollection;

        private async Task CreateIndexes()
        {
            await accountsCollection.Indexes.CreateOneAsync(
                 new CreateIndexModel<AccountModel>(Builders<AccountModel>.IndexKeys.Ascending(a => a.PubKey),
                 new CreateIndexOptions { Unique = true, Background = true })
            );
        }

        public override async Task OpenConnection(string connectionString)
        {
            var mongoUrl = new MongoUrl(connectionString);

            var conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
            ConventionRegistry.Register("IgnoreExtraElements", conventionPack, type => true);

            client = new MongoClient(mongoUrl);
            database = client.GetDatabase(mongoUrl.DatabaseName);

            ordersCollection = database.GetCollection<OrderModel>("orders");
            accountsCollection = database.GetCollection<AccountModel>("accounts");
            balancesCollection = database.GetCollection<BalanceModel>("balances");
            quantaCollection = database.GetCollection<QuantumModel>("quanta");
            constellationStateCollection = database.GetCollection<ConstellationState>("stellarData");
            withdrawalsCollection = database.GetCollection<WithdrawalModel>("withdrawals");

            effectsCollection = database.GetCollection<EffectModel>("effects");

            settingsCollection = database.GetCollection<SettingsModel>("constellationSettings");

            assetsCollection = database.GetCollection<AssetModel>("assets");

            await CreateIndexes();
        }

        public override Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public override async Task<List<WithdrawalModel>> LoadWithdrawals()
        {
            return await withdrawalsCollection
                .Find(FilterDefinition<WithdrawalModel>.Empty)
                .ToListAsync();
        }

        public override async Task<List<OrderModel>> LoadOrders()
        {
            return await ordersCollection
                .Find(FilterDefinition<OrderModel>.Empty)
                .ToListAsync();
        }

        public override async Task<SettingsModel> LoadSettings(long apex)
        {
            return await settingsCollection
                .Find(s => s.Apex <= apex)
                .SortByDescending(s => s.Apex)
                .FirstOrDefaultAsync();
        }

        public override async Task<List<AssetModel>> LoadAssets(long apex)
        {
            return await assetsCollection
                .Find(a => a.Apex <= apex)
                .ToListAsync();
        }

        public override async Task<List<AccountModel>> LoadAccounts()
        {
            return await accountsCollection
                .Find(FilterDefinition<AccountModel>.Empty)
                .ToListAsync();
        }

        public override async Task<List<BalanceModel>> LoadBalances()
        {
            return await balancesCollection
                .Find(FilterDefinition<BalanceModel>.Empty)
                .ToListAsync();
        }

        public override async Task<ConstellationState> LoadConstellationState()
        {
            return await constellationStateCollection
                .Find(FilterDefinition<ConstellationState>.Empty)
                .FirstOrDefaultAsync();
        }

        public override async Task<List<QuantumModel>> LoadQuanta(params long[] apexes)
        {
            var filter = FilterDefinition<QuantumModel>.Empty;
            if (apexes.Length > 0)
                filter = Builders<QuantumModel>.Filter.In(q => q.Apex, apexes);

            var res = await quantaCollection
                .Find(filter)
                .ToListAsync();

            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");

            return res;
        }

        public async Task<List<QuantumModel>> LoadQuantumModels(params long[] quantumIds)
        {
            return await quantaCollection
                .Find(Builders<QuantumModel>.Filter.In(q => q.Apex, quantumIds))
                .ToListAsync();
        }

        public override async Task<List<QuantumModel>> LoadQuantaAboveApex(long apex)
        {
            return await quantaCollection
                .Find(q => q.Apex > apex)
                .ToListAsync();
        }

        public override async Task<long> GetFirstEffectApex()
        {
            var firstEffect = await effectsCollection
                   .Find(FilterDefinition<EffectModel>.Empty)
                   .SortBy(e => e.Apex)
                   .FirstOrDefaultAsync();

            return firstEffect?.Apex ?? -1;
        }

        public override async Task<List<EffectModel>> LoadEffectsForApex(long apex)
        {
            return await effectsCollection
                .Find(e => e.Apex == apex)
                .ToListAsync();
        }

        public override async Task<List<EffectModel>> LoadEffectsAboveApex(long apex)
        {
            return await effectsCollection
                .Find(e => e.Apex > apex)
                .ToListAsync();
        }

        public override async Task<long> GetLastApex()
        {
            var constellationState = await constellationStateCollection
                .Find(FilterDefinition<ConstellationState>.Empty)
                .FirstOrDefaultAsync();

            return constellationState?.CurrentApex ?? -1;
        }

        #region Updates

        public override async Task Update(DiffObject update)
        {
            using (var session = await client.StartSessionAsync())
            {
                session.StartTransaction();

                try
                {
                    var updateTasks = new List<Task>();

                    if (update.Settings != null)
                    {
                        updateTasks.Add(settingsCollection.InsertOneAsync(update.Settings));
                        updateTasks.Add(assetsCollection.InsertManyAsync(update.Assets));
                    }
                    if (update.StellarInfoData != null)
                        updateTasks.Add(constellationStateCollection.BulkWriteAsync(GetStellarDataUpdate(update.StellarInfoData)));

                    updateTasks.Add(accountsCollection.BulkWriteAsync(GetAccountUpdates(update.Accounts)));

                    updateTasks.Add(balancesCollection.BulkWriteAsync(GetBalanceUpdates(update.Balances)));

                    updateTasks.Add(ordersCollection.BulkWriteAsync(GetOrderUpdates(update.Orders)));

                    updateTasks.Add(withdrawalsCollection.BulkWriteAsync(GetWithdrawalsUpdates(update.Widthrawals)));

                    updateTasks.Add(quantaCollection.InsertManyAsync(update.Quanta));
                    updateTasks.Add(effectsCollection.InsertManyAsync(update.Effects));

                    await session.CommitTransactionAsync();
                }
                catch
                {
                    await session.AbortTransactionAsync();
                    throw;
                }
            }
        }

        private WriteModel<WithdrawalModel>[] GetWithdrawalsUpdates(List<DiffObject.Withdrawal> widthrawals)
        {
            unchecked
            {
                var filter = Builders<WithdrawalModel>.Filter;
                var update = Builders<WithdrawalModel>.Update;

                var widthrawalsLength = widthrawals.Count;
                var updates = new WriteModel<WithdrawalModel>[widthrawalsLength];

                for (int i = 0; i < widthrawalsLength; i++)
                {
                    var widthrawal = widthrawals[i];
                    var apex = widthrawal.Apex;
                    var trHash = widthrawal.TransactionHash;
                    var raw = widthrawal.RawWithdrawal;
                    if (widthrawal.IsInserted)
                        updates[i] = new InsertOneModel<WithdrawalModel>(new WithdrawalModel
                        {
                            Apex = apex,
                            TransactionHash = trHash,
                            RawWithdrawal = raw
                        });
                    else if (widthrawal.IsDeleted)
                        updates[i] = new DeleteOneModel<WithdrawalModel>(filter.Eq(a => a.Apex, apex));
                    else
                        throw new InvalidOperationException("Withdrawal object cannot be updated");
                }
                return updates;
            }
        }

        private WriteModel<ConstellationState>[] GetStellarDataUpdate(DiffObject.ConstellationState constellationState)
        {
            var filter = Builders<ConstellationState>.Filter;
            var update = Builders<ConstellationState>.Update;

            var ledger = constellationState.Ledger;
            var vaultSequence = constellationState.VaultSequence;
            var apex = constellationState.CurrentApex;

            WriteModel<ConstellationState> updateModel = null;
            if (constellationState.IsInserted)
                updateModel = new InsertOneModel<ConstellationState>(new ConstellationState
                {
                    Ledger = ledger,
                    VaultSequence = vaultSequence,
                    CurrentApex = apex
                });
            else if (constellationState.IsDeleted)
                throw new InvalidOperationException("Stellar data entry cannot be deleted");
            else
            {
                UpdateDefinition<ConstellationState> updateCommand = update.Set(s => s.CurrentApex, apex);
                if (ledger > 0)
                    updateCommand = updateCommand.Set(s => s.Ledger, ledger);
                if (vaultSequence > 0)
                    updateCommand = updateCommand.Set(s => s.VaultSequence, vaultSequence);
                updateModel = new UpdateOneModel<ConstellationState>(filter.Empty, updateCommand);
            }

            return new WriteModel<ConstellationState>[] { updateModel };
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
                    var pubKey = acc.PubKey;
                    var currentAccFilter = filter.Eq(a => a.PubKey, pubKey);
                    if (acc.IsInserted)
                        updates[i] = new InsertOneModel<AccountModel>(new AccountModel { Nonce = (long)acc.Nonce, PubKey = pubKey });
                    else if (acc.IsDeleted)
                        updates[i] = new DeleteOneModel<AccountModel>(currentAccFilter);
                    else
                        updates[i] = new UpdateOneModel<AccountModel>(currentAccFilter, update.Set(a => a.Nonce, (long)acc.Nonce));
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
                var pubKey = balance.PubKey;
                var asset = balance.AssetId;
                var amount = balance.Amount;
                var liabilities = balance.Liabilities;

                var currentBalanceFilter = filter.And(filter.Eq(s => s.Account, pubKey), filter.Eq(b => b.AssetId, asset));

                if (balance.IsInserted)
                    updates[i] = new InsertOneModel<BalanceModel>(new BalanceModel
                    {
                        Account = pubKey,
                        Amount = amount,
                        AssetId = asset,
                        Liabilities = liabilities
                    });
                else if (balance.IsDeleted)
                    updates[i] = new DeleteOneModel<BalanceModel>(currentBalanceFilter);
                else
                {
                    updates[i] = new UpdateOneModel<BalanceModel>(
                        currentBalanceFilter,
                        update
                            .Inc(b => b.Amount, amount)
                            .Inc(b => b.Liabilities, liabilities)
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
                    var orderId = order.OrderId;
                    var price = order.Price;
                    var amount = order.Amount;
                    var pubKey = order.Pubkey;

                    var currentOrderFilter = filter.And(filter.Eq(s => s.OrderId, (long)orderId));

                    if (order.IsInserted)
                        updates[i] = new InsertOneModel<OrderModel>(new OrderModel
                        {
                            OrderId = (long)orderId,
                            Amount = amount,
                            Price = price,
                            Pubkey = pubKey
                        });
                    else if (order.IsDeleted)
                        updates[i] = new DeleteOneModel<OrderModel>(currentOrderFilter);
                    else
                    {
                        updates[i] = new UpdateOneModel<OrderModel>(
                            currentOrderFilter,
                            update.Inc(b => b.Amount, amount)
                        );
                    }
                }
                return updates;
            }
        }
        #endregion
    }
}
