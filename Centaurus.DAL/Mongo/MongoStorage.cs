using Centaurus.DAL.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
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
        private IMongoCollection<StellarData> stellarDataCollection;
        private IMongoCollection<WithdrawalModel> withdrawalsCollection;
        private IMongoCollection<EffectModel> effectsCollection;
        private IMongoCollection<SettingsModel> constellationSettingsCollection;
        private IMongoCollection<AssetSettingsModel> assetsCollection;

        private async Task CreateIndexes()
        {
            await effectsCollection.Indexes.CreateOneAsync(
                 new CreateIndexModel<EffectModel>(Builders<EffectModel>.IndexKeys.Ascending(a => a.Apex),
                 new CreateIndexOptions { Background = true })
            );

            await constellationSettingsCollection.Indexes.CreateOneAsync(
                 new CreateIndexModel<SettingsModel>(Builders<SettingsModel>.IndexKeys.Ascending(a => a.Apex),
                 new CreateIndexOptions { Unique = true, Background = true })
            );

            await assetsCollection.Indexes.CreateManyAsync(
                new CreateIndexModel<AssetSettingsModel>[] {
                    new CreateIndexModel<AssetSettingsModel>(Builders<AssetSettingsModel>.IndexKeys.Ascending(a => a.AssetId),
                        new CreateIndexOptions { Unique = true, Background = true }
                    ),
                    new CreateIndexModel<AssetSettingsModel>(
                        Builders<AssetSettingsModel>.IndexKeys.Combine(
                            Builders<AssetSettingsModel>.IndexKeys.Ascending(a => a.Code),
                            Builders<AssetSettingsModel>.IndexKeys.Ascending(a => a.Issuer)
                        ),
                        new CreateIndexOptions { Unique = true, Background = true }
                    )
                }
            );
        }

        public override async Task OpenConnection(string connectionString)
        {
            var mongoUrl = new MongoUrl(connectionString);

            client = new MongoClient(mongoUrl);
            database = client.GetDatabase(mongoUrl.DatabaseName);

            ordersCollection = database.GetCollection<OrderModel>("orders");
            accountsCollection = database.GetCollection<AccountModel>("accounts");
            balancesCollection = database.GetCollection<BalanceModel>("balances");
            quantaCollection = database.GetCollection<QuantumModel>("quanta");
            stellarDataCollection = database.GetCollection<StellarData>("stellarData");
            withdrawalsCollection = database.GetCollection<WithdrawalModel>("withdrawals");

            effectsCollection = database.GetCollection<EffectModel>("effects");

            constellationSettingsCollection = database.GetCollection<SettingsModel>("constellationSettings");

            assetsCollection = database.GetCollection<AssetSettingsModel>("assets");

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

        public override async Task<SettingsModel> LoadSettings(ulong apex)
        {
            var filter = Builders<SettingsModel>.Filter.Lte(s => s.Apex, apex);

            return await constellationSettingsCollection
                .Find(filter)
                .SortByDescending(s => s.Apex)
                .FirstOrDefaultAsync();
        }

        public override async Task<List<AssetSettingsModel>> LoadAssets(ulong apex)
        {
            return await assetsCollection
                .Find(Builders<AssetSettingsModel>.Filter.Lte(s => s.Apex, apex))
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

        public override async Task<StellarData> LoadStellarData()
        {
            return await stellarDataCollection
                .Find(FilterDefinition<StellarData>.Empty)
                .FirstOrDefaultAsync();
        }

        public override async Task<List<QuantumModel>> LoadQuanta(params ulong[] apexes)
        {
            var filter = FilterDefinition<QuantumModel>.Empty;
            if (apexes.Length > 0)
                filter = Builders<QuantumModel>.Filter.In(q => q.Apex, apexes);

            var res = await quantaCollection
                .Find(FilterDefinition<QuantumModel>.Empty)
                .ToListAsync();


            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");

            return res;
        }

        public async Task<List<QuantumModel>> LoadQuantumModels(params ulong[] quantumIds)
        {
            return await quantaCollection.Find(Builders<QuantumModel>.Filter.In(q => q.Apex, quantumIds)).ToListAsync();
        }

        public override async Task<List<EffectModel>> LoadEffectsForApex(ulong apex)
        {
            return await effectsCollection
                .Find(e => e.Apex >= apex)
                .ToListAsync();
        }

        public override async Task<List<EffectModel>> LoadEffectsAboveApex(ulong apex)
        {
            return await effectsCollection
                .Find(e => e.Apex > apex)
                .ToListAsync();
        }

        public override async Task<ulong> GetLastApex()
        {
            return await quantaCollection
                .Find(FilterDefinition<QuantumModel>.Empty)
                .SortByDescending(q => q.Apex)
                .Project(q => q.Apex)
                .FirstOrDefaultAsync();
        }

        #region Updates

        public override async Task Update(UpdateObject update)
        {
            using (var session = await client.StartSessionAsync())
            {
                session.StartTransaction();

                try
                {
                    var updateTasks = new List<Task>();

                    if (update.Settings != null)
                    {
                        updateTasks.Add(constellationSettingsCollection.InsertOneAsync(update.Settings));
                        updateTasks.Add(assetsCollection.InsertManyAsync(update.Assets));
                    }

                    if (update.StellarData != null)
                        updateTasks.Add(stellarDataCollection.BulkWriteAsync(GetStellarDataUpdate(update.StellarData)));

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

        private WriteModel<WithdrawalModel>[] GetWithdrawalsUpdates(List<WithdrawalCalcModel> widthrawals)
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
                var currentWidthrawalFilter = filter.Eq(a => a.Apex, apex);
                if (widthrawal.IsInserted)
                    updates[i] = new InsertOneModel<WithdrawalModel>(new WithdrawalModel { Apex = apex, TransactionHash = trHash });
                else if (widthrawal.IsDeleted)
                    updates[i] = new DeleteOneModel<WithdrawalModel>(currentWidthrawalFilter);
                else
                    throw new InvalidOperationException("Withdrawal object cannot be updated");
            }
            return updates;
        }

        private WriteModel<StellarData>[] GetStellarDataUpdate(StellarDataCalcModel stellarData)
        {
            var filter = Builders<StellarData>.Filter;
            var update = Builders<StellarData>.Update;

            var ledger = stellarData.Ledger;
            var vaultSequence = stellarData.VaultSequence;

            WriteModel<StellarData> updateModel = null;
            if (stellarData.IsInserted)
                updateModel = new InsertOneModel<StellarData>(new StellarData { Ledger = ledger, VaultSequence = vaultSequence });
            else if (stellarData.IsDeleted)
                throw new InvalidOperationException("Stellar data entry cannot be deleted");
            else
                updateModel = new UpdateOneModel<StellarData>(filter.Empty, update.Set(s => s.Ledger, ledger).Set(s => s.VaultSequence, vaultSequence));

            return new WriteModel<StellarData>[] { updateModel };
        }

        private WriteModel<AccountModel>[] GetAccountUpdates(List<AccountCalcModel> accounts)
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
                    updates[i] = new InsertOneModel<AccountModel>(new AccountModel { Nonce = acc.Nonce, PubKey = pubKey });
                else if (acc.IsDeleted)
                    updates[i] = new DeleteOneModel<AccountModel>(currentAccFilter);
                else
                    updates[i] = new UpdateOneModel<AccountModel>(currentAccFilter, update.Set(a => a.Nonce, acc.Nonce));
            }
            return updates;
        }

        private WriteModel<BalanceModel>[] GetBalanceUpdates(List<BalanceCalcModel> balances)
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

        private WriteModel<OrderModel>[] GetOrderUpdates(List<OrderCalcModel> orders)
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

                var currentOrderFilter = filter.And(filter.Eq(s => s.OrderId, orderId));

                if (order.IsInserted)
                    updates[i] = new InsertOneModel<OrderModel>(new OrderModel
                    {
                        OrderId = orderId,
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

        #endregion
    }
}
