using Centaurus.Models;
using Centaurus.PersistentStorage;

namespace Centaurus.Domain
{
    public static class BalancePersistenceModelExtionsions
    {
        public static Balance ToDomainModel(this BalancePersistentModel balance)
        {
            return new Balance
            {
                Asset = balance.Asset,
                Amount = balance.Amount
            };
        }

        public static BalancePersistentModel ToPersistentModel(this Balance balance)
        {
            return new BalancePersistentModel
            {
                Asset = balance.Asset,
                Amount = balance.Amount
            };
        }
    }
}
