using System.Collections.Generic;
using System.Text;
namespace Centaurus.Models
{
    public abstract class Quantum : Message
    {
        public override ulong MessageId => unchecked((ulong)Apex);

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
        /// Operation time stamp. Assigned by Alpha server when it finishes processing a quantum.
        /// </summary>
        [XdrField(2)]
        public long Timestamp { get; set; }

        /// <summary>
        /// If this property is set to false, then sync worker (thread that sends quantum to auditors) shouldn't send it yet
        /// </summary>
        public bool IsProcessed { get; set; } = true;
    }
}