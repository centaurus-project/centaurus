using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.PaymentProvider
{
    public class DepositNotificationManager
    {
        public DepositNotificationManager(string cursor, ICursorComparer cursorComparer)
        {
            Cursor = LastRegisteredCursor = cursor;
            CursorComparer = cursorComparer ?? throw new ArgumentNullException(nameof(cursorComparer));
        }

        public void RegisterNotification(DepositNotificationModel notification)
        {
            lock (notificationsSyncRoot)
            {
                var currentNotificationCursor = notification.Cursor;
                if (CursorComparer.CompareCursors(currentNotificationCursor, LastRegisteredCursor) > 0)
                {
                    pendingDeposits.Add(notification);
                    LastRegisteredCursor = currentNotificationCursor;
                }
            }
        }

        public bool TryGetNextPayment(out DepositNotificationModel notification)
        {
            lock (notificationsSyncRoot)
            {
                notification = pendingDeposits.FirstOrDefault();
                return notification != null;
            }
        }

        public void RemovePayment(string cursor)
        {
            lock (notificationsSyncRoot)
            {
                var notification = pendingDeposits.FirstOrDefault();
                if (notification == null || notification.Cursor != cursor)
                    throw new Exception("Unable to dequeue cursor.");
                pendingDeposits.RemoveAt(0);
            }
        }

        public string Cursor { get; set; }
        public string LastRegisteredCursor { get; private set; }

        public List<DepositNotificationModel> GetAll()
        {
            lock (notificationsSyncRoot)
            {
                return pendingDeposits.ToList();
            }
        }

        object notificationsSyncRoot = new { };

        List<DepositNotificationModel> pendingDeposits = new List<DepositNotificationModel>();

        private ICursorComparer CursorComparer { get; }
    }
}
