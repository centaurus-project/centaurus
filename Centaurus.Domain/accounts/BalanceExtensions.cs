using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class BalanceExtensions
    {
        public enum BalanceUpdateSign
        {
            Plus,
            Minus
        }

        public static void UpdateBalance(this Balance balance, ulong amount, UpdateSign balanceUpdateSign)
        {
            if (balanceUpdateSign == UpdateSign.Plus)
                balance.Amount += amount;
            else
                balance.Amount -= amount;
        }

        /// <summary>
        /// Lock funds on account balance.
        /// </summary>
        /// <param name="balance">Asset balance</param>
        /// <param name="amount">Amount to lock</param>
        public static void UpdateLiabilities(this Balance balance, ulong amount, UpdateSign balanceUpdateSign)
        {
            if (balanceUpdateSign == UpdateSign.Plus)
                balance.Liabilities += amount;
            else
                balance.Liabilities -= amount;

            if (balance.Liabilities > balance.Amount)
                throw new InvalidOperationException("Invalid liabilities update request. " + balance.ToString());
        }

        /// <summary>
        /// Check if account has sufficient asset balance
        /// </summary>
        /// <param name="balance">Asset balance</param>
        /// <param name="amount">Required amount</param>
        /// <returns></returns>
        public static bool HasSufficientBalance(this Balance balance, ulong amount, ulong minBalance)
        {
            if (amount <= 0) throw new ArgumentException("Invalid operation amount: " + amount);
            if (balance == null)
                return false;
            var availableBalance = balance.GetAvailableBalance();
            if (availableBalance < amount)
                return false;
            return availableBalance - amount >= minBalance;
        }

        /// <summary>
        /// Returns balance amount minus liabilities
        /// </summary>
        /// <param name="balance">Asset balance</param>
        /// <returns></returns>
        public static ulong GetAvailableBalance(this Balance balance)
        {
            if (balance == null)
                return 0;
            return balance.Amount - balance.Liabilities;
        }
    }
}
