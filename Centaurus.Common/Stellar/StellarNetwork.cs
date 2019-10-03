using stellar_dotnet_sdk;

namespace Centaurus
{
    public class StellarNetwork
    {
        public StellarNetwork(string passphrase, string horizon)
        {
            Network = new Network(passphrase);

            Network.Use(Network);

            Horizon = horizon;

            Server = new Server(Horizon);
        }

        public Server Server { get; }

        public Network Network { get; }

        public string Horizon { get; }
    }
}
