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
        public static List<Task> WriteBatch<T>(this IMongoCollection<T> collection, IClientSessionHandle session, IList<WriteModel<T>> documents, CancellationToken ct, int batchSize = 5_000)
        {
            var position = 0;
            var updateTasks = new List<Task>();
            while (position < documents.Count)
            {
                updateTasks.Add(collection.BulkWriteAsync(
                    session,
                    documents.Skip(position).Take(batchSize),
                    new BulkWriteOptions { BypassDocumentValidation = true, IsOrdered = false },
                    ct)
                );
                position += batchSize;
            }

            return updateTasks;
        }
    }
}
