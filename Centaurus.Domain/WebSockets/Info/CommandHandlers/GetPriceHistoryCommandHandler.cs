using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class GetPriceHistoryCommandHandler : BaseCommandHandler<GetPriceHistoryCommand>
    {
        public GetPriceHistoryCommandHandler(ExecutionContext context)
            : base(context)
        {

        }

        public override BaseResponse Handle(InfoWebSocketConnection infoWebSocket, GetPriceHistoryCommand command)
        {
            var asset = Context.ConstellationSettingsManager.Current.Assets.FirstOrDefault(a => a.Code == command.Market);
            if (asset == null || asset.Code == Context.ConstellationSettingsManager.Current.QuoteAsset.Code)
                throw new BadRequestException("Invalid market.");

            var res = Context.AnalyticsManager.PriceHistoryManager.GetPriceHistory(command.Cursor, command.Market, command.Period);
            return new PriceHistoryResponse  { 
                RequestId = command.RequestId,
                PriceHistory  = res.frames,
                NextCursor = res.nextCursor
            };
        }
    }
}
