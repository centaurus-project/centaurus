using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class WithdrawalWrapperExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentTime">Time in unix time seconds</param>
        /// <returns></returns>
        public static bool IsExpired(this WithdrawalWrapper withdrawal, long currentTime)
        {
            return currentTime - withdrawal.MaxTime > ExecutionContext.MaxTxSubmitDelay;
        }
    }
}
