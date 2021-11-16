
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Centaurus.Domain
{
    public abstract class BaseCommand
    {
        public static JsonSerializerOptions SerializeOptions { get; }

        static BaseCommand()
        {
            SerializeOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            SerializeOptions.Converters.Add(new CommandWrapperConverter());
            SerializeOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public long RequestId { get; set; }

        public string Command { get; set; }

        public static BaseCommand Deserialize(ReadOnlySpan<byte> request)
        {
            var obj = JsonSerializer.Deserialize<CommandWrapper>(request, SerializeOptions);
            return obj.CommandObject;
        }
    }
}
