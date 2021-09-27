using System;
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

        CursorsPersistentModel LoadCursors();

        SettingsPersistentModel LoadSettings(ulong fromApex);

        QuantumPersistentModel LoadQuantum(ulong apex);

        List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes);

        IEnumerable<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex);

        List<AccountQuantumDTO> LoadQuantaForAccount(byte[] accountPubkey, ulong apex, int limit, QueryOrder order = QueryOrder.Asc);

        IEnumerable<PriceHistoryFramePersistentModel> GetPriceHistory(string market, int period, int from, int to);

        PendingQuantaPersistentModel LoadPendingQuanta();

        void DeletePendingQuanta();

        void SaveBatch(List<IPersistentModel> batch);
    }
}
