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

        [Option("auditor", Required = false, HelpText = "Genesis auditor settings. Each item must contain auditor's public key and domain in format {publicKey}={domain:port}. The option can be set multiple times.")]
        public IEnumerable<Auditor> GenesisAuditors { get; set; }

        [Option("listening_port", Required = false, HelpText = "Port the alpha will listen on.")]
        public int ListeningPort { get; set; }

        [Option("cert_path", Required = false, HelpText = "Certificate path.")]
        public string TlsCertificatePath { get; set; }

        [Option("cert_pk_path", Required = false, HelpText = "Certificate private key file path.")]
        public string TlsCertificatePrivateKeyPath { get; set; }

        [Option("sync_batch_size", Default = 500, HelpText = "Max quanta sync batch size.")]
        public int SyncBatchSize { get; set; }

        [Option("participation_level", Default = 0, HelpText = "Centaurus node participation level.")]
        public int ParticipationLevel { get; set; }

        [Option("payment_config", Required = false, HelpText = "Payment providers config path.")]
        public string PaymentConfigPath { get; set; }

        /// <summary>
        /// If current server configured to use secure connection than we assume that all constellation nodes are configure secure connection
        /// </summary>
        public bool UseSecureConnection => !string.IsNullOrWhiteSpace(TlsCertificatePath);

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

                UriBuilder = new UriBuilder(parseResult.uri);
                PubKey = parseResult.keyPair;
            }

            public Auditor(KeyPair pubKey, string address)
            {
                PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));
                if (!TryCreateUri(address, out var uri))
                    throw new ArgumentException("Invalid address");
                UriBuilder = new UriBuilder(uri);
            }

            public KeyPair PubKey { get; }

            private readonly UriBuilder UriBuilder;

            private (bool res, KeyPair keyPair, Uri uri) TryParse(string settings)
            {
                var splitted = settings.Split('=');
                if (splitted.Length != 2
                    || !StrKey.IsValidEd25519PublicKey(splitted[0])
                    || !TryCreateUri(splitted[1], out var uri))
                    return (false, null, null);
                return (true, KeyPair.FromAccountId(splitted[0]), uri);
            }

            private bool TryCreateUri(string address, out Uri uri)
            {
                return Uri.TryCreate($"http://{address}", UriKind.Absolute, out uri);
            }

            public Uri GetWsConnection(bool isSecureConnection)
            {
                UriBuilder.Scheme = isSecureConnection ? "wss" : "ws";
                return UriBuilder.Uri;
            }

            public Uri GetHttpConnection(bool isSecureConnection)
            {
                UriBuilder.Scheme = isSecureConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
                return UriBuilder.Uri;
            }
        }
    }
}
