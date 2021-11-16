using System;
using System.Linq;
using System.Reflection;
using RocksDbSharp;

namespace Centaurus.PersistentStorage
{
    internal class StorageOptions
    {
        public StorageOptions()
        {
            InitOptions();
            InitColumnFamilies();
        }

        internal DbOptions RocksDbOptions { get; private set; }
        internal ColumnFamilies RocksDbColumnFamilies { get; private set; }

        private void InitOptions()
        {
            RocksDbOptions = new DbOptions()
                .SetCreateIfMissing()
                .SetCreateMissingColumnFamilies()
                .SetMaxBackgroundCompactions(6)
                .SetMaxBackgroundFlushes(3)
                .SetDbWriteBufferSize(128 << 20) //128MB
                .IncreaseParallelism(Math.Max(Environment.ProcessorCount - 1, 1)); //TODO: adjust the available number of cores based on the other node parts needs
            // .EnableStatistics();
        }

        private void InitColumnFamilies()
        {
            var models = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IPersistentModel)))
                .Select(t => Activator.CreateInstance(t) as IPersistentModel);

            var sharedCache = Cache.CreateLru(1 << 30); //1GB

            //cfo.OptimizeLevelStyleCompaction();???
            //cfo.OptimizeUniversalStyleCompaction ()??? https://github.com/facebook/rocksdb/wiki/Rocksdb-Architecture-Guide

            RocksDbColumnFamilies = new ColumnFamilies();
            foreach (var model in models)
            {
                var tableOptions = new BlockBasedTableOptions()
                    .SetBlockCache(sharedCache)
                    .SetBlockSize(16 << 10)//16KB
                    .SetCacheIndexAndFilterBlocks(true)
                    .SetPinL0FilterAndIndexBlocksInCache(true);

                var cfo = new ColumnFamilyOptions()
                    .SetCompression(Compression.Lz4)
                    .SetLevelCompactionDynamicLevelBytes(true);

                if (model is IBloomFilteredPersistentModel)
                {
                    cfo.SetMemtablePrefixBloomSizeRatio(0.1);
                    tableOptions.SetFilterPolicy(BloomFilterPolicy.Create(8));
                }

                if (model is IPrefixedPersistentModel prefixed)
                {
                    cfo.SetPrefixExtractor(SliceTransform.CreateFixedPrefix(prefixed.PrefixLength));
                }

                cfo.SetBlockBasedTableFactory(tableOptions);
                RocksDbColumnFamilies.Add(new ColumnFamilies.Descriptor(model.ColumnFamily, cfo));
            }
        }
    }
}
