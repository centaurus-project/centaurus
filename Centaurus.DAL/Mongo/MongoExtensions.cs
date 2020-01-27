using MongoDB.Driver;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Mongo
{
    public static class MongoExtensions
    {
        public static ReplaceOneModel<T>[] GenerateBulkUpdateOperations<T>(Func<T, FilterDefinition<T>> filter, params T[] models)
        {
            var modelsLength = models.Length;
            var bulkOperations = new ReplaceOneModel<T>[modelsLength];
            for (var i = 0; i < modelsLength; i++)
            {
                var model = models[i];
                var upsertOperation = new ReplaceOneModel<T>(filter(model), model) { IsUpsert = true };
                bulkOperations[i] = (upsertOperation);
            }
            return bulkOperations;
        }
    }
}
