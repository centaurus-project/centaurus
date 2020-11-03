﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class InfoCommandsHandlers
    {
        static ImmutableDictionary<string, IBaseCommandHandler> handlers;

        static InfoCommandsHandlers()
        {
            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(IBaseCommandHandler).IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var processors = new Dictionary<string, IBaseCommandHandler>(StringComparer.OrdinalIgnoreCase);
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = Activator.CreateInstance(processorType) as IBaseCommandHandler;
                var constraints = processorType.GetGenericParameterConstraints();
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
                processors.Add(commandAttribute.Command, instance);
            }
            handlers = processors.ToImmutableDictionary();
        }

        public async Task<BaseResponse> HandleCommand(InfoWebSocketConnection infoWebSocket, BaseCommand command)
        {
            if (!handlers.ContainsKey(command.Command))
                throw new NotSupportedException($"Command {command.Command} is not supported.");
            return await handlers[command.Command].Handle(infoWebSocket, command);
        }
    }
}
