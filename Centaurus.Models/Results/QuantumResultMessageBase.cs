using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public abstract class QuantumResultMessageBase : ResultMessageBase, IQuantumInfoContainer
    {
        [XdrField(0)]
        public List<EffectsInfoBase> Effects { get; set; }

        [XdrField(1)]
        public PayloadProof PayloadProof { get; set; }

        [XdrField(2)]
        public RequestInfoBase Request { get; set; }

        [XdrField(3)]
        public ulong Apex { get; set; }

        public byte[] QuantumHash
        {
            get 
            {
                if (Request is RequestInfo)
                    return Request.Data.ComputeHash();
                return Request.Data;
            }
        }
    }
}