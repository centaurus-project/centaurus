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

        /// <summary>
        /// Adds the notification to the notifications queue
        /// </summary>
        /// <param name="notification"></param>
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

        /// <summary>
        /// Fetches first notification in the queue
        /// </summary>
        /// <param name="notification"></param>
        /// <returns>True if there are notifications in the queue, otherwise false</returns>
        public bool TryGetNextNotification(out DepositNotificationModel notification)
        {
            lock (notificationsSyncRoot)
            {
                notification = pendingDeposits.FirstOrDefault();
                return notification != null;
            }
        }

        /// <summary>
        /// Removes first notification in the queue
        /// </summary>
        public void RemoveNextNotification()
        {
            lock (notificationsSyncRoot)
            {
                pendingDeposits.RemoveAt(0);
            }
        }

        /// <summary>
        /// Marks all notifications as unsent
        /// </summary>
        public void ResetSentNotification()
        {
            lock (notificationsSyncRoot)
            {
                pendingDeposits.ForEach(n => n.IsSend = false);
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
