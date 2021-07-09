namespace Centaurus.PersistentStorage
{
    public partial class StorageQuery
    {
        public ulong GetLastApex()
        {
            return storage.Last<QuantumPersistentModel>().Apex;
        }

        public StorageIterator<AccountPersistentModel> LoadAccounts()
        {
            return storage.Find<AccountPersistentModel>();
        }

        public AccountPersistentModel LoadAccount(byte[] accountPubkey)
        {
            return storage.Get<AccountPersistentModel>(accountPubkey);
        }

        //DO we retain the settings history?
        public SettingsPersistentModel LoadSettings(ulong fromApex)
        {
            return storage.Find<SettingsPersistentModel>(ApexConverter.EncodeApex(fromApex)).Reverse().First();
        }


        //DO we retain the settings history?
        public StorageIterator<ProviderCursorPersistentModel> LoadCursors(ulong fromApex)
        {
            return storage.Find<ProviderCursorPersistentModel>();
        }
    }
}
