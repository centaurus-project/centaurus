using Centaurus.Domain.Quanta.Sync;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class SyncCursorUpdateExtensions
    {
        public static SyncCursorType ToDomainCursorType(this XdrSyncCursorType syncCursorType)
        {
            switch (syncCursorType)
            {
                case XdrSyncCursorType.Quanta:
                    return SyncCursorType.Quanta;
                case XdrSyncCursorType.Signatures:
                    return SyncCursorType.Signatures;
                default:
                    throw new ArgumentNullException($"{syncCursorType} cursor type is not supported.");
            }
        }
    }
}
