using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Centaurus.Domain
{
    public class CommandWrapperConverter : JsonConverter<CommandWrapper>
    {
        static CommandWrapperConverter()
        {
            var commands = new Dictionary<string, Type>();
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var t in allTypes)
            {
                if (t.IsAbstract || t.IsInterface || !typeof(BaseCommand).IsAssignableFrom(t))
                    continue;
                var commandAttribute = t.GetCustomAttribute<CommandAttribute>();
                if (commandAttribute == null)
                    continue;
                commands.Add(commandAttribute.Command, t);
            }
            CommandWrapperConverter.commands = commands.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private static ImmutableDictionary<string, Type> commands;

        public override CommandWrapper Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Reader should be set to the beginning.");
            }

            if (!reader.Read()
                    || reader.TokenType != JsonTokenType.PropertyName
                    || !reader.GetString().Equals(nameof(CommandWrapper.Command), StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException("First property should be command.");
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Command property value must be string.");
            }
            var command = reader.GetString();
            if (!commands.TryGetValue(command, out var commandType))
                throw new NotSupportedException($"Command {command} is not supported.");
            if (!reader.Read()
                    || reader.TokenType != JsonTokenType.PropertyName
                    || !reader.GetString().Equals(nameof(CommandWrapper.CommandObject), StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException("Second property must be CommandObject.");
            }
            reader.Read();//move reader to the start of nested object
            var commandObj = (BaseCommand)JsonSerializer.Deserialize(ref reader, commandType, options);
            commandObj.Command = command;
            var obj = new CommandWrapper { Command = command, CommandObject = commandObj };

            do reader.Read();
            while (reader.TokenType != JsonTokenType.EndObject);

            return obj;
        }

        public override void Write(Utf8JsonWriter writer, CommandWrapper value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}