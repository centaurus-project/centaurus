using System;

namespace Centaurus.PersistentStorage
{
    public partial class StorageQuery
    {
        public ulong GetLastApex()
        {
            return storage.Last<QuantumPersistentModel>()?.Apex ?? 0;
        }

        public StorageIterator<AccountPersistentModel> LoadAccounts()
        {
            return storage.Find<AccountPersistentModel>();
        }

        public AccountPersistentModel LoadAccount(byte[] accountPubkey)
        {
            return storage.Get<AccountPersistentModel>(accountPubkey);
        }

        public SettingsPersistentModel LoadSettings(ulong fromApex)
        {
            return storage.Find<SettingsPersistentModel>(ApexConverter.EncodeApex(fromApex), QueryOrder.Desc).First();
        }

        public CursorsPersistentModel LoadCursors()
        {
            return storage.First<CursorsPersistentModel>();
        }
    }
}
