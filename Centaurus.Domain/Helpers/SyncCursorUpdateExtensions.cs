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
                case XdrSyncCursorType.MajoritySignatures:
                    return SyncCursorType.MajoritySignatures;
                case XdrSyncCursorType.SingleNodeSignatures:
                    return SyncCursorType.SingleNodeSignatures;
                default:
                    throw new ArgumentNullException($"{syncCursorType} cursor type is not supported.");
            }
        }
    }
}
