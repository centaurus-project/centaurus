using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class UnsubscribeFromMarketCommandHandler : BaseCommandHandler<UnsubscribeFromMarket>
    {
        public override Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, UnsubscribeFromMarket command)
        {
            var subscriptionId = OHLCManager.EncodeAssetTradesResolution(command.Market, command.Period);
            if (infoWebSocket.Subscriptions.Contains(subscriptionId))
                infoWebSocket.Subscriptions.Remove(subscriptionId);
            return Task.FromResult((BaseResponse)new SuccesResponse { RequestId = command.RequestId });
        }
    }
}
