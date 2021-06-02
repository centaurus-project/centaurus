using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentNotificationManager
    {
        public PaymentNotificationManager(string cursor, PaymentsParserBase paymentsParser)
        {
            Cursor = cursor;
            PaymentsParser = paymentsParser ?? throw new ArgumentNullException(nameof(paymentsParser));
        }

        public void RegisterNotification(PaymentNotification notification)
        {
            lock (notificationsSyncRoot)
            {
                var currentNotificationCursor = notification.Cursor;
                if (PaymentsParser.CompareCursors(currentNotificationCursor, LastRegisteredCursor) > 0)
                {
                    pendingNotifications.Add(new PaymentNotificationWrapper(notification, DateTime.UtcNow));
                    LastRegisteredCursor = currentNotificationCursor;
                }
            }
        }

        public bool TryGetNextPayment(out PaymentNotificationWrapper notification)
        {
            lock (notificationsSyncRoot)
            {
                notification = pendingNotifications.FirstOrDefault();
                return notification == null;
            }
        }

        public void RemovePayment(string cursor)
        {
            lock (notificationsSyncRoot)
            {
                var notification = pendingNotifications.FirstOrDefault();
                if (notification == null || notification.Payment.Cursor != cursor)
                    throw new Exception("Unable to dequeue cursor.");
                pendingNotifications.RemoveAt(0);
            }
        }

        public string Cursor { get; set; }
        public string LastRegisteredCursor { get; private set; }
        public PaymentsParserBase PaymentsParser { get; }

        public List<PaymentNotificationWrapper> GetAll()
        {
            lock (notificationsSyncRoot)
            {
                return pendingNotifications.ToList();
            }
        }

        object notificationsSyncRoot = new { };

        List<PaymentNotificationWrapper> pendingNotifications = new List<PaymentNotificationWrapper>();
    }
}
