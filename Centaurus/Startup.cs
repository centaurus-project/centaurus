using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus
{
    interface IStartup<T>
        where T: BaseSettings
    {
        void Run(T settings);
        void Shutdown();
    }
}
