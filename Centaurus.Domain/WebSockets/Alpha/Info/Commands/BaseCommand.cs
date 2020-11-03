using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseCommand
    {
        private static JsonSerializerSettings settings;

        static BaseCommand()
        {
            settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects };
            settings.Converters.Add(new CommandsConverter());
        }

        public long RequestId { get; set; }

        public string Command { get; set; }

        public static BaseCommand Deserialize(byte[] request)
        {
            var stringifiedRequest = Encoding.UTF8.GetString(request);
            return JsonConvert.DeserializeObject<BaseCommand>(stringifiedRequest, settings);
        }
    }
}
