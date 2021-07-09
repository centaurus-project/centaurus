using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain.WebSockets.Alpha.Info.CommandHandlers
{
    public class SubscribeCommandHandler : BaseCommandHandler<SubscribeCommand>
    {
        public SubscribeCommandHandler(ExecutionContext context)
            : base(context)
        {

        }
        public override BaseResponse Handle(InfoWebSocketConnection infoWebSocket, SubscribeCommand command)
        {
            if (command.Subscriptions == null || command.Subscriptions.Count < 1)
                throw new BadRequestException("At least one subscription must be specified.");

            foreach (var subsName in command.Subscriptions)
            {

                infoWebSocket.AddSubscription(Context.SubscriptionsManager.GetOrAddSubscription(BaseSubscription.GetBySubscriptionName(subsName)));
            }
            return new SuccesResponse();
        }
    }
}
