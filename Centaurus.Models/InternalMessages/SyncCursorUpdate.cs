using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    //TODO: convert ulong to ulong? after migration to MessagePack
    [XdrContract]
    public class SyncCursorUpdate : Message
    {
        [XdrField(0)]
        public ulong QuantaCursor { get; set; } = ulong.MaxValue;

        [XdrField(1)]
        public ulong SignaturesCursor { get; set; } = ulong.MaxValue;
    }
}