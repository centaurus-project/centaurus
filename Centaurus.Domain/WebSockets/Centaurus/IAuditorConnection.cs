using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static Centaurus.Domain.StateManager;

namespace Centaurus.Domain
{
    public interface IAuditorConnection
    {
        RawPubKey PubKey { get; }

        AuditorState AuditorState { get; }
    }
}
