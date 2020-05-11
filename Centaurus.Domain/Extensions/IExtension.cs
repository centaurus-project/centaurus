using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public interface IExtension: IDisposable
    {
        void Init(Dictionary<string, string> settings);
    }
}
