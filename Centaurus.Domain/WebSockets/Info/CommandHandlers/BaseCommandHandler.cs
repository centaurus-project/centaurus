using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public interface IBaseCommandHandler
    {
        public abstract BaseResponse Handle(InfoWebSocketConnection infoWebSocket, object command);
    }

    public interface IBaseCommandHandler<T>
        where T : BaseCommand
    {
        public abstract BaseResponse Handle(InfoWebSocketConnection infoWebSocket, T command);
    }

    public abstract class BaseCommandHandler<T>: ContextualBase, IBaseCommandHandler<T>, IBaseCommandHandler
        where T : BaseCommand
    {
        public BaseCommandHandler(ExecutionContext context)
            :base(context)
        {

        }

        public abstract BaseResponse Handle(InfoWebSocketConnection infoWebSocket, T command);

        BaseResponse IBaseCommandHandler.Handle(InfoWebSocketConnection infoWebSocket, object command)
        {
            return Handle(infoWebSocket, (T)command);
        }
    }
}
