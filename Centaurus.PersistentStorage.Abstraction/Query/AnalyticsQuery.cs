using System;
using System.Buffers.Binary;

namespace Centaurus.PersistentStorage
{
    public partial class StorageQuery
    {
        public StorageIterator<PriceHistoryFramePersistentModel> GetPriceHistory(int cursorTimeStamp, int toUnixTimeStamp, int period, string asset)
        {
            return storage.Find<PriceHistoryFramePersistentModel>(EncodeKeyPrefix(cursorTimeStamp, period)).Reverse();
        }
        
        private byte[] EncodeKeyPrefix(int timestamp, int period)
        {
            var key = new byte[8];
            BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(0, 4), period);
            BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(4, 4), timestamp);
            return key;
        }
    }
}
