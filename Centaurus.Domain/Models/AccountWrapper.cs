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
            Account = account;
        }

        public Account Account { get; }
    }
}
