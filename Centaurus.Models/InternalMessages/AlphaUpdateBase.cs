using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class AlphaUpdateBase: ConstellationRequestMessage
    {
        [XdrField(0)]
        public RawPubKey Alpha { get; set; }
    }
}
