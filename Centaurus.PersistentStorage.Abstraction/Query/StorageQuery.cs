
namespace Centaurus.PersistentStorage
{
    public partial class StorageQuery
    {
        internal readonly PersistentStorage storage;

        internal StorageQuery(PersistentStorage storage)
        {
            this.storage = storage;
        }
    }
}
