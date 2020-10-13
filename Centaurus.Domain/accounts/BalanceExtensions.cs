﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class BalanceExtensions
    {
        public static void UpdateBalance(this Balance balance, long amount)
        {
            balance.Amount += amount;
            if (balance.Amount < 0) throw new InvalidOperationException("Negative asset balance after update. " + balance.ToString());
        }

        /// <summary>
        /// Lock funds on account balance.
        /// </summary>
        /// <param name="balance">Asset balance</param>
        /// <param name="amount">Amount to lock</param>
        public static void UpdateLiabilities(this Balance balance, long amount)
        {
            balance.Liabilities += amount;
            if (balance.Liabilities > balance.Amount || balance.Liabilities < 0) throw new InvalidOperationException("Invalid liabilities update request. " + balance.ToString());
        }

        /// <summary>
        /// Check if account has sufficient asset balance
        /// </summary>
        /// <param name="balance">Asset balance</param>
        /// <param name="amount">Required amount</param>
        /// <returns></returns>
        public static bool HasSufficientBalance(this Balance balance, long amount)
        {
            if (amount <= 0) throw new ArgumentException("Invalid operation amount: " + amount);
            if (balance == null)
                return false;
            return balance.Amount - balance.Liabilities >= amount;
        }
    }
}
