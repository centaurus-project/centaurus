namespace Centaurus.DAL.Models
{
    public class AccountModel
    {
        public byte[] PubKey { get; set; }

        //it stores ulong
        public long Nonce { get; set; }
    }
}
