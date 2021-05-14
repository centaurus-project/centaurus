using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Centaurus
{
    public class XdrObjectConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.GetCustomAttributes(typeof(XdrContractAttribute), true).Length > 0;
        }

        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var props = value.GetType().GetProperties().ToList();
            writer.WriteStartObject();
            foreach (var prop in props)
            {
                if (!prop.GetCustomAttributes(false).Any(c => c is XdrFieldAttribute))
                    continue;

                writer.WritePropertyName(prop.Name);
                JsonSerializer.Serialize(writer, prop.GetValue(value), prop.PropertyType, options);
            }
            writer.WriteEndObject();
        }

        public static string Convert(object xdrObject)
        {
            return JsonSerializer.Serialize(xdrObject, Options);
        }

        private static JsonSerializerOptions options;
        private static JsonSerializerOptions Options
        {
            get
            {
                if (options == null)
                {
                    options = new JsonSerializerOptions { IgnoreNullValues = true };
                    options.Converters.Add(new XdrObjectConverter());
                }
                return options;
            }
        }
    }
}
