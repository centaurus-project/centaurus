using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class SyncCursorReset : Message
    {
        [XdrField(0)]
        public List<SyncCursor> Cursors { get; set; } = new List<SyncCursor>();
    }
}