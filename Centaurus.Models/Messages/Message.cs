using System;

namespace Centaurus.Models
{
    public abstract class Message: IXdrSerializableModel
    {
        public abstract MessageTypes MessageType { get; }

        public virtual ulong MessageId { get { return 0; } }
    }
}
