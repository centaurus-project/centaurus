using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain.WebSockets.Alpha.Info.CommandHandlers
{
    public class SubscribeCommandHandler : BaseCommandHandler<SubscribeCommand>
    {
        public override Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, SubscribeCommand command)
        {
            if (command.Subscriptions.Count < 1)
                throw new BadRequestException("At least one subscription must be specified.");

            foreach (var subsName in command.Subscriptions)
            {

                infoWebSocket.AddSubscription(SubscriptionsManager.GetOrAddSubscription(BaseSubscription.GetBySubscriptionName(subsName)));
            }
            return Task.FromResult((BaseResponse)new SuccesResponse());
        }
    }
}
