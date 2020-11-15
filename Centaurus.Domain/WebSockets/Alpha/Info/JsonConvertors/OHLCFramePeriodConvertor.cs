using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Centaurus.Domain
{
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
                if (!EnumExtensions.GetValues<OHLCFramePeriod, int>().Any(v => v == intVal))
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
