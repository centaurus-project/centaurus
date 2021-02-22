using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class Quantum : Message
    {
        public override long MessageId => Apex;

        /// <summary>
        /// Unique sequential quantum id. Assigned by Alpha server.
        /// </summary>
        [XdrField(0)]
        public long Apex { get; set; }

        /// <summary>
        /// Previous quantum hash.
        /// </summary>
        [XdrField(1)]
        public byte[] PrevHash { get; set; }

        /// <summary>
        /// Current quantum effects hash.
        /// </summary>
        [XdrField(2)]
        public byte[] EffectsHash { get; set; }

        /// <summary>
        /// Operation time stamp. Assigned by Alpha server when it finishes processing a quantum.
        /// </summary>
        [XdrField(3)]
        public long Timestamp { get; set; }
    }
}