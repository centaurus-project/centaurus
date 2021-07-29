using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class DepositModelExtensions
    {
        public static DepositNotification ToDomainModel(this DepositNotificationModel notificationModel)
        {
            if (notificationModel == null)
                throw new ArgumentNullException(nameof(notificationModel));

            return new DepositNotification
            {
                Cursor = notificationModel.Cursor,
                Items = notificationModel.Items.Select(i => i.ToDomainModel()).ToList(),
                ProviderId = notificationModel.ProviderId
            };
        }

        public static Deposit ToDomainModel(this DepositModel depositModel)
        {
            if (depositModel == null)
                throw new ArgumentNullException(nameof(depositModel));

            return new Deposit
            {
                Amount = depositModel.Amount,
                Asset = depositModel.Asset,
                Destination = depositModel.Destination,
                PaymentResult = depositModel.IsSuccess ? PaymentResults.Success : PaymentResults.Failed,
                TransactionHash = depositModel.TransactionHash
            };
        }
    }
}
