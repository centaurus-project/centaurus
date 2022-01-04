using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus
{
    public class Settings
    {
        public KeyPair KeyPair { get; private set; }

        [Option("verbose", Default = false, HelpText = "Logs all messages. The verbose option overrides the silent one.")]
        public bool Verbose { get; set; }

        [Option("silent", Default = false, HelpText = "Logs only errors.")]
        public bool Silent { get; set; }

        [Option('s', "secret", Required = true, HelpText = "Current application secret key.")]
        public string Secret { get; set; }

        [Option("cwd", Required = true, HelpText = "Working directory for logs and other files.")]
        public string CWD { get; set; }

        [Option("connection_string", Required = true, HelpText = "Database connection string.")]
        public string ConnectionString { get; set; }

        [Option("extensions_config_file_path", Required = false, HelpText = "Path to extensions config file.")]
        public string ExtensionsConfigFilePath { get; set; }

        [Option("auditor", Required = false, HelpText = "Genesis auditor settings. Each item must contain auditor's public key and domain in format {publicKey}={domain:port}. The option can be set multiple times.", Separator = ';')]
        public IEnumerable<Auditor> GenesisAuditors { get; set; }

        [Option("listening_port", Required = false, HelpText = "Port the node will listen on.")]
        public int ListeningPort { get; set; }

        [Option("cert_path", Required = false, HelpText = "Certificate path.")]
        public string TlsCertificatePath { get; set; }

        [Option("cert_pk_path", Required = false, HelpText = "Certificate private key file path.")]
        public string TlsCertificatePrivateKeyPath { get; set; }

        [Option("sync_batch_size", Default = 500, HelpText = "Max quanta sync batch size.")]
        public int SyncBatchSize { get; set; }

        [Option("catchup_timeout", Default = 15, HelpText = "Catchup timeout in seconds.")]
        public int CatchupTimeout { get; set; }

        [Option("participation_level", Required = true, HelpText = "Centaurus node participation level. '1' or 'Prime' for the prime nodes, and '2' or 'Auditor' for the auditor nodes.")]
        public ParticipationLevel ParticipationLevel { get; set; }

        [Option("payment_config", Required = true, HelpText = "Payment providers config path.")]
        public string PaymentConfigPath { get; set; }

        [Option("use_secure_connection", Default = false, HelpText = "Use https/wss or not.")]
        public bool UseSecureConnection { get; set; }

        public void Build()
        {
            KeyPair = KeyPair.FromSecretSeed(Secret);
        }

        public class Auditor
        {
            public Auditor(string auditorSettings)
            {
                if (string.IsNullOrWhiteSpace(auditorSettings))
                    throw new ArgumentNullException(nameof(auditorSettings));

                var parseResult = TryParse(auditorSettings);
                if (!parseResult.res)
                    throw new ArgumentNullException("Invalid genesis_domains options.");

                PubKey = parseResult.keyPair;
                Address = parseResult.address;
            }

            public Auditor(KeyPair pubKey, string address)
            {
                PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));

                if (address == null)
                    return;
                if (!UriHelper.TryCreateUri(address, false, out _))
                    throw new ArgumentException("Invalid address");
                Address = address;
            }

            public KeyPair PubKey { get; }
            public string Address { get; }

            private (bool res, KeyPair keyPair, string address) TryParse(string settings)
            {
                var splitted = settings.Split('=');
                var pubkeySeed = splitted[0];
                var address = splitted[1];
                if (splitted.Length != 2
                    || !StrKey.IsValidEd25519PublicKey(pubkeySeed)
                    || !string.IsNullOrEmpty(address) && !UriHelper.TryCreateUri(address, false, out _))
                    return (false, null, null);
                return (true, KeyPair.FromAccountId(pubkeySeed), address);
            }

            public bool IsPrime => !string.IsNullOrEmpty(Address);

            public Uri GetWsConnection(bool isSecureConnection)
            {
                UriHelper.TryCreateWsConnection(Address, isSecureConnection, out var uri);
                return uri;
            }

            public Uri GetHttpConnection(bool isSecureConnection)
            {
                UriHelper.TryCreateHttpConnection(Address, isSecureConnection, out var uri);
                return uri;
            }
        }
    }
}
