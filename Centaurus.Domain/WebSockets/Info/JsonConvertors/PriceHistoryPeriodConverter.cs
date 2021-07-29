using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Centaurus.Domain
{
    public class PriceHistoryPeriodConverter : JsonConverter<PriceHistoryPeriod>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            var val = typeof(PriceHistoryPeriod) == typeToConvert;
            return val;
        }

        public override PriceHistoryPeriod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                reader.Read();
                if (!Enum.TryParse<PriceHistoryPeriod>(reader.GetString(), out var value))
                    throw new JsonException($"Unable to parse \"{reader.GetString()}\" as OHLCFramePeriod.");
                return value;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                reader.Read();
                var intVal = reader.GetInt32();
                if (!Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>().Any(v => v == (PriceHistoryPeriod)intVal))
                    throw new JsonException($"Unable to parse \"{intVal}\" as OHLCFramePeriod.");
                return (PriceHistoryPeriod)intVal;
            }
            throw new JsonException($"{reader.TokenType} is not valid type for OHLCFramePeriod.");
        }

        public override void Write(Utf8JsonWriter writer, PriceHistoryPeriod value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
