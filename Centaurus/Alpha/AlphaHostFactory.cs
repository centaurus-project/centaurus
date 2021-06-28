using Centaurus.Domain;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Alpha
{
    public abstract class AlphaHostFactoryBase
    {
        public abstract IHost GetHost(ExecutionContext context);

        public static AlphaHostFactoryBase Default { get; } = new AlphaHostFactory();
    }

    public class AlphaHostFactory: AlphaHostFactoryBase
    {
        public override IHost GetHost(ExecutionContext context)
        {
            return new AlphaHostBuilder(context)
                .CreateHost();
        }
    }
}
