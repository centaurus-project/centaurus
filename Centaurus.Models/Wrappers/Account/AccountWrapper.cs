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

        public Account Account { get; }

        public AccountRequestCounter RequestCounter { get; }
    }
}
