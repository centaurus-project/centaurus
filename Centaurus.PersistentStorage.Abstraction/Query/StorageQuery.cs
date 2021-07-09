
namespace Centaurus.PersistentStorage
{
    public enum QueryResultsOrder
    {
        Asc,
        Desc
    }

    public partial class StorageQuery
    {
        internal readonly PersistentStorage storage;

        internal StorageQuery(PersistentStorage storage)
        {
            this.storage = storage;
        }
    }
}
