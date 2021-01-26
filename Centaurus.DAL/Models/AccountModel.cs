using MongoDB.Bson.Serialization.Attributes;

namespace Centaurus.DAL.Models
{
    public class AccountModel
    {
        public int Id { get; set; }

        public byte[] PubKey { get; set; }

        public long Nonce { get; set; }

        public long Withdrawal { get; set; }

        public RequestRateLimitsModel RequestRateLimits { get; set; }
    }
}
