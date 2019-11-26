using Centaurus.DAL.Models;
using System.Collections.Generic;

namespace Centaurus.DAL
{
    public class UpdateObject
    {
        public StellarDataCalcModel StellarData { get; set; }
        public List<AccountCalcModel> Accounts { get; set; }
        public List<BalanceCalcModel> Balances { get; set; }
        public List<EffectModel> Effects { get; set; }
        public List<QuantumModel> Quanta { get; set; }
        public List<OrderCalcModel> Orders { get; set; }
        public List<WithdrawalCalcModel> Widthrawals { get; set; }
        public SettingsModel Settings { get; set; }
        public List<AssetSettingsModel> Assets { get; set; }
    }
}
