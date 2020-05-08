using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public interface IExtension
    {
        Task Init(Dictionary<string, string> settings);

        Task Terminate();
    }
}
