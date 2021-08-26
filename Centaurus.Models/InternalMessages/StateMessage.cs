using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class StateUpdateMessage : Message
    {
        [XdrField(0)]
        public State State { get; set; }

        [XdrField(1)]
        public bool IsPrime { get; set; }
    }
}