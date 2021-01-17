using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    interface IStartup<T>
        where T: BaseSettings
    {
        Task Run(T settings, ManualResetEvent resetEvent);
        Task Shutdown();
    }
}
