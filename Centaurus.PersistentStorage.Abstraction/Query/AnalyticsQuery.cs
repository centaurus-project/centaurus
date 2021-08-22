using System;
using System.Buffers.Binary;
using System.Text;

namespace Centaurus.PersistentStorage
{
    public partial class StorageQuery
    {
        /// <summary>
        /// Fetch price history for a given market and snapshot period.
        /// </summary>
        /// <param name="market">An asset to fetch history for</param>
        /// <param name="period">Time snapshot period</param>
        /// <param name="from">Unix timestamp to start from (inclusive)</param>
        /// <param name="to">Unix timestamp to look up to (exclusive)</param>
        /// <returns>Price history for a given timespan</returns>
        public StorageIterator<PriceHistoryFramePersistentModel> GetPriceHistory(string market, int period, int from, int to)
        {
            var toBoundary = EncodeKeyPrefix(market, period, to);
            var fromBoundary = EncodeKeyPrefix(market, period, from);
            if (from > to)
            {
                return storage.Find<PriceHistoryFramePersistentModel>(fromBoundary, toBoundary, QueryOrder.Desc);
            }
            return storage.Find<PriceHistoryFramePersistentModel>(fromBoundary, toBoundary);
        }

        private byte[] EncodeKeyPrefix(string market, int period, int timestamp)
        {
            var encodedMarket = Encoding.UTF8.GetBytes(market);
            var key = new byte[12];
            encodedMarket.CopyTo(key.AsSpan(0, 4));
            BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(4, 4), period);
            BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(8, 4), timestamp);
            return key;
        }
    }
}
