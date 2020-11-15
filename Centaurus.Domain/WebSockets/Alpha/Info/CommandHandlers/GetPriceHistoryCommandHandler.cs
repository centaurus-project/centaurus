using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class GetPriceHistoryCommandHandler : BaseCommandHandler<GetPriceHistory>
    {
        public override async Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, GetPriceHistory command)
        {
            var asset = Global.Constellation.Assets.FirstOrDefault(a => a.Id == command.Market);
            if (asset == null && asset.IsXlm)
                throw new BadRequestException("Invalid market.");

            var res = (await Global.AnalyticsManager.OHLCManager.GetPeriod(command.Cursor, command.Market, command.Period));
            return new PriceHistoryResponse  { 
                RequestId = command.RequestId,
                PriceHistory  = Global.AnalyticsManager.TradesHistoryManager.GetTrades(command.Market),
                NextCursor = res.nextCursor
            };
        }
    }
}
