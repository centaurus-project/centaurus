﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage.Abstraction
{
    public interface IPersistentStorage : IDisposable
    {
        void Connect(string path);

        ulong GetLastApex();

        IEnumerable<AccountPersistentModel> LoadAccounts();

        AccountPersistentModel LoadAccount(byte[] accountPubkey);

        List<ProviderCursorPersistentModel> LoadCursors();

        SettingsPersistentModel LoadSettings(ulong fromApex);

        QuantumPersistentModel LoadQuantum(ulong apex);

        List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes);

        IEnumerable<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex);

        List<QuantumPersistentModel> LoadQuantaForAccount(byte[] accountPubkey, ulong apex, int limit, QueryResultsOrder order = QueryResultsOrder.Asc);

        List<QuantumPersistentModel> LoadQuantaForAccount(ulong accountId, ulong fromApex, int limit, QueryResultsOrder order = QueryResultsOrder.Asc);

        IEnumerable<PriceHistoryFramePersistentModel> GetPriceHistory(int cursorTimeStamp, int toUnixTimeStamp, int period, string asset);

        void SaveBatch(List<IPersistentModel> batch);
    }
}
