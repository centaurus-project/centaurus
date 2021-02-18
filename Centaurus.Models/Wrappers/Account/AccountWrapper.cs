using System;

namespace Centaurus.Models
{
    public class AccountWrapper
    {
        public AccountWrapper(Account account, RequestRateLimits requestRateLimits)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));
            RequestCounter = new AccountRequestCounter(Account, requestRateLimits);
        }

        public int Id => Account.Id;

        public Account Account { get; }

        public AccountRequestCounter RequestCounter { get; }

        public WithdrawalWrapper Withdrawal { get; set; }

        public bool HasPendingWithdrawal => Withdrawal != null;
    }
}
