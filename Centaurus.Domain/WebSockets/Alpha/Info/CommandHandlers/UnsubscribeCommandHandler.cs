using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class UnsubscribeCommandHandler : BaseCommandHandler<UnsubscribeCommand>
    {
        public UnsubscribeCommandHandler(ExecutionContext context)
            :base(context)
        {

        }

        public override Task<BaseResponse> Handle(InfoWebSocketConnection infoWebSocket, UnsubscribeCommand command)
        {
            if (command.Subscriptions.Count < 0)
                throw new BadRequestException("At least one subscription must be specified.");

            foreach (var subs in command.Subscriptions)
            {
                var subscription = Context.SubscriptionsManager.GetOrAddSubscription(BaseSubscription.GetBySubscriptionName(subs));
                infoWebSocket.RemoveSubsctioption(subscription);
            }
            return Task.FromResult((BaseResponse)new SuccesResponse { RequestId = command.RequestId });
        }
    }
}
