﻿using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class GetMarketCommandHandler : BaseCommandHandler<GetMarket>
    {
        public override async Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, GetMarket command)
        {
            var asset = Global.Constellation.Assets.FirstOrDefault(a => a.Id == command.Market);
            if (asset == null && asset.IsXlm)
                throw new BadRequestException("Invalid market.");
            var subscriptionId = OHLCManager.EncodeManagerId(command.Market, command.Period);
            if (command.SubscribeToUpdates && !infoWebSocket.Subscriptions.Contains(subscriptionId))
                infoWebSocket.Subscriptions.Add(subscriptionId);

            var frames = (await Global.AnalyticsManager.OHLCManager.GetPeriod(command.DateFrom, command.DateTo, command.Market, command.Period)).ToList();
            return new MarketResponse { 
                RequestId = command.RequestId,
                Frames = frames,
                Trades = Global.AnalyticsManager.TradesHistoryManager.GetTrades(command.Market)
            };
        }
    }
}
