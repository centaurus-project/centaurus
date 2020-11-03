using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace Centaurus.Domain
{
    public class CommandsConverter : JsonConverter
    {
        static CommandsConverter()
        {
            var commands = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
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
            CommandsConverter.commands = commands.ToImmutableDictionary();
        }

        private static ImmutableDictionary<string, Type> commands;

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(BaseCommand).IsAssignableFrom(typeToConvert);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            string command = (string)obj["command"];

            if (!commands.ContainsKey(command))
                throw new NotSupportedException($"{command} is not supported.");

            BaseCommand commandObj = (BaseCommand)Activator.CreateInstance(commands[command]);

            serializer.Populate(obj.CreateReader(), commandObj);

            return commandObj;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}