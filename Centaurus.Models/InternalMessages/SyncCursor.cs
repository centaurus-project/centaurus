using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class SyncCursor
    {
        [XdrField(0)]
        public ulong Cursor { get; set; }

        [XdrField(1)]
        public XdrSyncCursorType Type { get; set; }

        /// <summary>
        /// Set to false, to cancel sync.
        /// </summary>
        [XdrField(2)]
        public bool DisableSync { get; set; }
    }

    public enum XdrSyncCursorType
    {
        Quanta = 0,
        MajoritySignatures = 1,
        SingleNodeSignatures = 2
    }
}
