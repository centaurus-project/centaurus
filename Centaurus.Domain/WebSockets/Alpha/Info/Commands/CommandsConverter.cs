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

        public override bool CanConvert(Type typeToConvert)
        {
            return base.CanConvert(typeToConvert);
        }

        public override CommandWrapper Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Reader should be set to the beginning.");
            }

            if (!reader.Read()
                    || reader.TokenType != JsonTokenType.PropertyName
                    || !reader.GetString().Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException("First property should be command.");
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Command property value should be string.");
            }
            var command = reader.GetString();
            if (!commands.TryGetValue(command, out var commandType))
                throw new NotSupportedException($"Command {command} is not supported.");
            if (!reader.Read()
                    || reader.TokenType != JsonTokenType.PropertyName
                    || !reader.GetString().Equals("commandObject", StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException("Second property should be CommandObject.");
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

    public class OHLCFramePeriodConverter : JsonConverter<OHLCFramePeriod>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            var val = typeof(OHLCFramePeriod) == typeToConvert;
            return val;
        }

        public override OHLCFramePeriod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                reader.Read();
                if (!Enum.TryParse<OHLCFramePeriod>(reader.GetString(), out var value))
                    throw new JsonException($"Unable to parse \"{reader.GetString()}\" as OHLCFramePeriod.");
                return value;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                reader.Read();
                    var intVal = reader.GetInt32();
                if (!Enum.GetValues(typeof(OHLCFramePeriod)).Cast<int>().Any(v => v == intVal))
                    throw new JsonException($"Unable to parse \"{intVal}\" as OHLCFramePeriod.");
                return (OHLCFramePeriod)intVal;
            }
            throw new JsonException($"{reader.TokenType} is not valid type for OHLCFramePeriod.");
        }

        public override void Write(Utf8JsonWriter writer, OHLCFramePeriod value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}