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
                Task.Factory.StartNew(ObserveUpdates, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
                if (exc is TaskCanceledException || exc is OperationCanceledException)
                    return;
                logger.Error(exc);
            }
        }

        private Dictionary<int, Market> Markets = new Dictionary<int, Market>();

        private BlockingCollection<ExchangeUpdate> awaitedUpdates;

        public OrderMap OrderMap { get; } = new OrderMap();
        public Action<OrderInfo> OnNewOrder { get; internal set; }
        public Action<OrderInfo> OnOrderRemoved { get; internal set; }

        /// <summary>
        /// Process customer's order request.
        /// </summary>
        /// <param name="orderRequest">Order request quantum</param>
        /// <returns></returns>
        public void ExecuteOrder(EffectProcessorsContainer effectsContainer)
        {
            RequestQuantum orderRequestQuantum = (RequestQuantum)effectsContainer.Envelope.Message;
            var orderRequest = (OrderRequest)orderRequestQuantum.RequestEnvelope.Message;
            var updates = new OrderMatcher(orderRequest, effectsContainer).Match();
            awaitedUpdates?.Add(updates);
        }

        public event Action<ExchangeUpdate> OnUpdates;

        public Market GetMarket(int asset)
        {
            if (Markets.TryGetValue(asset, out Market market)) return market;
            throw new InvalidOperationException($"Asset {asset} is not supported");
        }

        public bool HasMarket(int asset)
        {
            return Markets.ContainsKey(asset);
        }

        public Market AddMarket(AssetSettings asset)
        {
            var market = asset.CreateMarket(OrderMap);
            Markets.Add(asset.Id, market);
            return market;
        }

        public void Clear()
        {
            Markets.Clear();
            OrderMap.Clear();
        }

        public Orderbook GetOrderbook(ulong offerId)
        {
            var parts = OrderIdConverter.Decode(offerId);
            return GetOrderbook(parts.Asset, parts.Side);
        }

        public Orderbook GetOrderbook(int asset, OrderSide side)
        {
            return GetMarket(asset).GetOrderbook(side);
        }

        public Order GetOrder(ulong offerId)
        {
            return GetOrderbook(offerId).GetOrder(offerId);
        }

        public bool RemoveOrder(ulong offerId)
        {
            return GetOrderbook(offerId).RemoveOrder(offerId);
        }

        public static Exchange RestoreExchange(List<AssetSettings> assets, List<Order> orders, bool observeTrades)
        {
            var exchange = new Exchange(observeTrades);
            foreach (var asset in assets)
                exchange.AddMarket(asset);

            foreach (var order in orders)
            {
                var orderData = OrderIdConverter.Decode(order.OrderId);
                var market = exchange.GetMarket(orderData.Asset);
                var orderbook = market.GetOrderbook(orderData.Side);
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
