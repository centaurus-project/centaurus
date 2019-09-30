using stellar_dotnet_sdk;

namespace Centaurus
{
    public class StellarNetwork
    {
        public StellarNetwork(string passphrase, string horizon)
        {
            Network = new Network(passphrase);

            Network.Use(Network);

            Server = new Server(horizon);
        }

        public Server Server { get; }

        public Network Network { get; }
    }
}
