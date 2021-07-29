using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface IAuditorConnection
    {
        RawPubKey PubKey { get; }
    }
}
