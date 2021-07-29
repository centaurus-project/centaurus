using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class InfoCommandsHandlers: ContextualBase
    {
        ImmutableDictionary<string, IBaseCommandHandler> handlers;

        public InfoCommandsHandlers(ExecutionContext context)
            :base(context)
        {
                var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(x => typeof(IBaseCommandHandler).IsAssignableFrom(x)
                        && !x.IsInterface
                        && !x.IsAbstract);

                var processors = new Dictionary<string, IBaseCommandHandler>();
                foreach (var processorType in discoveredRequestProcessors)
                {
                    Type baseCommandHandlerClass = processorType;
                    while(baseCommandHandlerClass.Name != typeof(BaseCommandHandler<>).Name //TODO: find more elegant and reliable way to check generic types
                        && baseCommandHandlerClass.BaseType != null)
                        baseCommandHandlerClass = processorType.BaseType;

                    if (!baseCommandHandlerClass.IsGenericType)
                        return;

                    var constraints = baseCommandHandlerClass.GenericTypeArguments;
                    if (constraints.Length != 1
                        || !typeof(BaseCommand).IsAssignableFrom(constraints[0])
                        || constraints[0].IsAbstract
                        || constraints[0].IsInterface)
                        throw new Exception("Info command handler should be constrained with one of info command types.");

                    var commandType = constraints[0];

                    var commandAttribute = commandType.GetCustomAttribute<CommandAttribute>();
                    if (commandAttribute == null)
                        throw new Exception($"Info command {commandType.Name} is missing InfoCommandAttribute.");

                    if (processors.ContainsKey(commandAttribute.Command))
                        throw new Exception($"Handler for command type {commandAttribute.Command} is already registered");
                    var instance = Activator.CreateInstance(processorType, new object[] { Context }) as IBaseCommandHandler;
                    processors.Add(commandAttribute.Command, instance);
                }
                handlers = processors.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public BaseResponse HandleCommand(InfoWebSocketConnection infoWebSocket, BaseCommand command)
        {
            if (!handlers.ContainsKey(command.Command))
                throw new NotSupportedException($"Command {command.Command} is not supported.");
            return handlers[command.Command].Handle(infoWebSocket, command);
        }
    }
}
