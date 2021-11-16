using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class OrderRemovedEffect: BaseOrderEffect
    {
        [XdrField(0)]
        public ulong OrderId { get; set; }
    }
}
