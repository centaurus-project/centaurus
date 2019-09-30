using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    /// <summary>
    /// Contains all quanta that were processed after the last snapshot
    /// </summary>
    public class PendingQuanta: IXdrSerializableModel
    {
        public List<MessageEnvelope> Quanta { get; set; } = new List<MessageEnvelope>();

        public byte[] ToByteArray()
        {
            return XdrConverter.Serialize(this);
        }

        public static PendingQuanta FromByteArray(byte[] rawData)
        {
            if (rawData == null)
                throw new ArgumentNullException(nameof(rawData));

            return XdrConverter.Deserialize<PendingQuanta>(rawData);
        }
    }
}
