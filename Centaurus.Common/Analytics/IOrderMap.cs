using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Analytics
{
    public interface IOrderMap
    {
        /// <summary> 
        /// </summary>
        /// <param name="currentOrderId">If equal to default, first order will be returned.</param>
        /// <returns>Next order</returns>
        public OrderInfo GetNextOrder(ulong currentOrderId);
    }
}
