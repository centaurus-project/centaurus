using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class TxCursorManager
    {
        public TxCursorManager(long txCursor)
        {
            TxCursor = txCursor;
        }

        private object syncRoot = new { };

        public void SetCursor(long cursor)
        {
            lock (syncRoot)
                TxCursor = cursor;
        }

        public bool IsValidNewCursor(long newCursor)
        {
            lock (syncRoot)
                return TxCursor <= newCursor;
        }

        public long TxCursor { get; private set; }
    }
}
