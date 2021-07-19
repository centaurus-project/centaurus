using System;
using System.Collections.Generic;
using System.Linq;
using RocksDbSharp;

namespace Centaurus.PersistentStorage
{
    public class PersistentStorage : IDisposable
    {
        private RocksDb db;
        private StorageOptions options;
        private Dictionary<string, ColumnFamilyHandle> columnFamilies;

        public PersistentStorage(string dbPath)
        {
            Connect(dbPath);
        }

        private void Connect(string dbPath)
        {
            options = new StorageOptions();
            db = RocksDb.Open(options.RocksDbOptions, dbPath, options.RocksDbColumnFamilies);
            columnFamilies = options.RocksDbColumnFamilies.ToDictionary(cf => cf.Name, cf => db.GetColumnFamily(cf.Name));
        }

        public void SaveBatch(List<IPersistentModel> modelsToSave)
        {
            using (var batch = new WriteBatch())
            {
                foreach (var obj in modelsToSave)
                {
                    batch.Put(obj.Key, obj.SerializeValue(), columnFamilies[obj.ColumnFamily]);
                }

                db.Write(batch, new WriteOptions());
            }
        }

        public StorageIterator<T> Find<T>() where T : IPersistentModel, new()
        {
            var iterator = db.NewIterator(ResolveColumnFamily<T>(), new ReadOptions().SetTotalOrderSeek(true));
            return new StorageIterator<T>(iterator);
        }

        public StorageIterator<T> Find<T>(byte[] prefix) where T : IPersistentModel, new()
        {
            var iterator = db.NewIterator(ResolveColumnFamily<T>(), new ReadOptions().SetPrefixSameAsStart(true).SetTotalOrderSeek(false));
            return new StorageIterator<T>(iterator.Seek(prefix));
        }

        //TODO: fork the rocksdb wrapper and add rocksdb_readoptions_set_iterate_lower_bound support,as well as NET 5.0 deploy fix (see https://github.com/curiosity-ai/rocksdb-sharp/pulls)
        public StorageIterator<T> Find<T>(byte[] prefix, byte[] upperBound) where T : IPersistentModel, new()
        {
            var iterator = db.NewIterator(ResolveColumnFamily<T>(), new ReadOptions().SetPrefixSameAsStart(true).SetTotalOrderSeek(false).SetIterateUpperBound(upperBound));
            return new StorageIterator<T>(iterator.Seek(prefix));
        }

        public T First<T>() where T : IPersistentModel, new()
        {
            using (var iterator = db.NewIterator(ResolveColumnFamily<T>(),
                new ReadOptions().SetReadaheadSize(0).SetTotalOrderSeek(true)))
            {
                if (!iterator.Valid()) return default(T); //not found - return null
                return IPersistentModel.Deserialize<T>(iterator.Key(), iterator.Value());
            }
        }

        public T Last<T>() where T : IPersistentModel, new()
        {
            using (var iterator = db.NewIterator(ResolveColumnFamily<T>(),
                new ReadOptions().SetTotalOrderSeek(true)))
            {
                iterator.SeekToLast();
                if (!iterator.Valid()) return default(T); //not found - return null
                return IPersistentModel.Deserialize<T>(iterator.Key(), iterator.Value());
            }
        }

        public T Get<T>(byte[] key) where T : IPersistentModel, new()
        {
            var value = db.Get(key, ResolveColumnFamily<T>());
            return IPersistentModel.Deserialize<T>(key, value);
        }

        public T Get<T>(T keyRef) where T : IPersistentModel, new()
        {
            return Get<T>(keyRef.Key);
        }

        public List<T> MutliGet<T>(byte[][] keys) where T : IPersistentModel, new()
        {
            var cfs = new ColumnFamilyHandle[keys.Length];
            Array.Fill(cfs, ResolveColumnFamily<T>());
            KeyValuePair<byte[], byte[]>[] items = db.MultiGet(keys, cfs);
            var res = new List<T>(items.Length);
            foreach (var item in items)
            {
                item.Deconstruct(out byte[] key, out byte[] value);
                res.Add(IPersistentModel.Deserialize<T>(key, value));
            }
            return res;
        }

        public List<T> MutliGet<T>(IEnumerable<T> keyRefs) where T : IPersistentModel, new()
        {
            return MutliGet<T>(keyRefs.Select(t => t.Key).ToArray());
        }

        //implement single item fetch and multiget

        public string GetStats()
        {
            return options.RocksDbOptions.GetStatisticsString();
        }

        private ColumnFamilyHandle ResolveColumnFamily<T>() where T : IPersistentModel, new()
        {
            var cf = new T().ColumnFamily;
            return columnFamilies[cf];
        }

        public void Dispose()
        {
            db?.Dispose();
        }
    }
}
