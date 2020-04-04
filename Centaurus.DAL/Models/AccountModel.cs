namespace Centaurus.DAL.Models
{
    public class AccountModel
    {
        public byte[] PubKey { get; set; }

        public long Nonce { get; set; }

        public RequestRateLimitsModel RequestRateLimits { get; set; }
    }
}
