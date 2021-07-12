using Centaurus.Domain.Models;
using Centaurus.Models;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class Exchange : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        public Exchange(bool observeTrades)
        {
            if (observeTrades)
            {
                cancellationTokenSource = new CancellationTokenSource();
                cancellationToken = cancellationTokenSource.Token;
                awaitedUpdates = new BlockingCollection<ExchangeUpdate>();
                Task.Factory.StartNew(ObserveUpdates, TaskCreationOptions.LongRunning);
            }
        }


        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        private void ObserveUpdates()
        {
            try
            {
                foreach (var updates in awaitedUpdates.GetConsumingEnumerable(cancellationToken))
                    OnUpdates?.Invoke(updates);
            }
            catch (Exception exc)
            {
                if (exc is OperationCanceledException)
                    return;
                logger.Error(exc);
            }
        }

        private Dictionary<string, ExchangeMarket> Markets = new Dictionary<string, ExchangeMarket>();

        private BlockingCollection<ExchangeUpdate> awaitedUpdates;

        public OrderMap OrderMap { get; } = new OrderMap();

        /// <summary>
        /// Process customer's order request.
        /// </summary>
        /// <param name="orderRequest">Order request quantum</param>
        /// <returns></returns>
        public void ExecuteOrder(RequestContext context)
        {
            var orderRequest = (OrderRequest)context.Request.RequestMessage;
            var updates = new OrderMatcher(orderRequest, context).Match();
            awaitedUpdates?.Add(updates);
        }

        public void RemoveOrder(RequestContext context, OrderbookBase orderbook, OrderWrapper order)
        {
            context.AddOrderRemoved(orderbook, order, context.Context.Constellation.GetBaseAsset());
            if (awaitedUpdates != null)
            {
                var updateTime = new DateTime(context.Quantum.Timestamp, DateTimeKind.Utc);
                var exchangeItem = new ExchangeUpdate(orderbook.Market, updateTime);
                exchangeItem.OrderUpdates.Add(order.Order.ToOrderInfo(OrderState.Deleted));
                awaitedUpdates.Add(exchangeItem);
            }
        }

        public event Action<ExchangeUpdate> OnUpdates;

        public ExchangeMarket GetMarket(string asset)
        {
            if (Markets.TryGetValue(asset, out ExchangeMarket market)) return market;
            throw new InvalidOperationException($"Asset {asset} is not supported");
        }

        public bool HasMarket(string asset)
        {
            return Markets.ContainsKey(asset);
        }

        public ExchangeMarket AddMarket(AssetSettings asset, bool useLegacyOrderbook = false)
        {
            var market = asset.CreateMarket(OrderMap, useLegacyOrderbook);
            Markets.Add(asset.Code, market);
            return market;
        }

        public void Clear()
        {
            Markets.Clear();
            OrderMap.Clear();
        }

        public OrderbookBase GetOrderbook(string asset, OrderSide side)
        {
            return GetMarket(asset).GetOrderbook(side);
        }

        public static Exchange RestoreExchange(List<AssetSettings> assets, List<OrderWrapper> orders, bool observeTrades, bool useLegacyOrderbook = false)
        {
            var exchange = new Exchange(observeTrades);
            foreach (var asset in assets)
                exchange.AddMarket(asset, useLegacyOrderbook);

            foreach (var order in orders)
            {
                var orderbook = exchange.GetOrderbook(order.Order.Asset, order.Order.Side);
                orderbook.InsertOrder(order);
            }
            return exchange;
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            awaitedUpdates?.Dispose();
            awaitedUpdates = null;
        }
    }
}
