using System.Collections.Generic;
using System.Text;
namespace Centaurus.Models
{
    public abstract class Quantum : Message
    {
        /// <summary>
        /// Unique sequential quantum id. Assigned by Alpha server.
        /// </summary>
        public ulong Apex { get; set; }

        /// <summary>
        /// Operation timestamp. Assigned by Alpha server when it finishes processing a quantum.
        /// </summary>
        public ulong Timestamp { get; set; }

        public override ulong MessageId => Apex;

        /// <summary>
        /// If this property is set to false, than sync worker (thread that sends quantum to auditors) shouldn't send it yet
        /// </summary>
        public bool IsProcessed { get; set; } = true;
    }
}