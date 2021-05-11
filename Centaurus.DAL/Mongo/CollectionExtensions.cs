using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.DAL.Mongo
{
    public static class CollectionExtensions
    {
        public static List<Task> SaveBatch<T>(this IMongoCollection<T> collection, IClientSessionHandle session, IEnumerable<WriteModel<T>> documents, CancellationToken ct, int batchSize = 5_000)
        {
            var savedCount = 0;
            var itemsCount = documents.Count();
            var updateTasks = new List<Task>();
            while (savedCount < itemsCount)
            {
                updateTasks.Add(collection.BulkWriteAsync(
                    session,
                    documents.Skip(savedCount).Take(batchSize),
                    new BulkWriteOptions { BypassDocumentValidation = true, IsOrdered = false },
                    ct)
                );
                savedCount += batchSize;
            }

            return updateTasks;
        }

        public static List<Task> InsertBatch<T>(this IMongoCollection<T> collection, IClientSessionHandle session, IEnumerable<T> documents, CancellationToken ct, int batchSize = 5_000)
        {
            var savedCount = 0;
            var itemsCount = documents.Count();
            var updateTasks = new List<Task>();
            while (savedCount < itemsCount)
            {
                updateTasks.Add(collection.InsertManyAsync(
                    session,
                    documents.Skip(savedCount).Take(batchSize),
                    new InsertManyOptions { BypassDocumentValidation = true, IsOrdered = false },
                    ct)
                );
                savedCount += batchSize;
            }

            return updateTasks;
        }
    }
}
