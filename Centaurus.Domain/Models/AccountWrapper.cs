using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class AccountWrapper
    {
        public AccountWrapper(Account account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));
            Account = account;
            RequestCounter = new AccountRequestCounter(Account);
        }

        public Account Account { get; }

        public AccountRequestCounter RequestCounter { get; }
    }
}
