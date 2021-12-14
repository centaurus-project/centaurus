using Centaurus.Models;

namespace Centaurus.Domain
{
    internal interface INodeConnection
    {
        RawPubKey PubKey { get; }

        RemoteNode Node { get; }
    }
}
