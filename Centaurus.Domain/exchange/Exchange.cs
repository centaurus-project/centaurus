using Centaurus.Analytics;
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
                awaitedTrades = new BlockingCollection<List<Trade>>();
                Task.Factory.StartNew(ObserveTrades, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }


        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private void ObserveTrades()
        {
            try
            {
                foreach (var trades in awaitedTrades.GetConsumingEnumerable(cancellationTokenSource.Token))
                    OnTrade?.Invoke(trades);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }

        private Dictionary<int, Market> Markets = new Dictionary<int, Market>();

        private BlockingCollection<List<Trade>> awaitedTrades;

        public OrderMap OrderMap { get; } = new OrderMap();

        /// <summary>
        /// Process customer's order request.
        /// </summary>
        /// <param name="orderRequest">Order request quantum</param>
        /// <returns></returns>
        public void ExecuteOrder(EffectProcessorsContainer effectsContainer)
        {
            RequestQuantum orderRequestQuantum = (RequestQuantum)effectsContainer.Envelope.Message;
            var orderRequest = (OrderRequest)orderRequestQuantum.RequestEnvelope.Message;
            var trades = new OrderMatcher(orderRequest, effectsContainer).Match();
            awaitedTrades?.Add(trades);
        }

        public event Action<List<Trade>> OnTrade;

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

        public Orderbook GetOrderbook(int asset, OrderSides side)
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
            cancellationTokenSource = null;
            awaitedTrades?.Dispose();
            awaitedTrades = null;
        }
    }
}
