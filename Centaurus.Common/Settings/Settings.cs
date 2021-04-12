using CommandLine;
using Newtonsoft.Json;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Centaurus
{
    public abstract class BaseSettings
    {
        public KeyPair KeyPair { get; set; }

        [Option("verbose", Default = false, HelpText = "Logs all messages. The verbose option overrides the silent one.")]
        public bool Verbose { get; set; }

        [Option("silent", Default = false, HelpText = "Logs only errors.")]
        public bool Silent { get; set; }

        [Option('s', "secret", Required = true, HelpText = "Current application secret key.")]
        public string Secret { get; set; }

        [Option("cwd", Required = true, HelpText = "Working directory for logs and other files.")]
        public string CWD { get; set; }

        [Option("network_passphrase", Required = true, HelpText = "Stellar network passphrase.")]
        public string NetworkPassphrase { get; set; }

        [Option("horizon_url", Required = true, HelpText = "URL of Stellar horizon.")]
        public string HorizonUrl { get; set; }

        [Option("connection_string", Required = true, HelpText = "Database connection string.")]
        public string ConnectionString { get; set; }

        [Option("extensions_config_file_path", Required = false, HelpText = "Path to extensions config file.")]
        public string ExtensionsConfigFilePath { get; set; }

        public virtual void Build()
        {
            KeyPair = KeyPair.FromSecretSeed(Secret);
        }
    }

    [Verb("auditor", HelpText = "Launch Centaurus in auditor mode.")]
    public class AuditorSettings : BaseSettings
    {
        [Option("alpha_address", Required = true, HelpText = "URL of Alpha server.")]
        public string AlphaAddress { get; set; }

        [Option("alpha_pubkey", Required = true, HelpText = "Alpha server public key.")]
        public string AlphaPubKey { get; set; }

        [Option("genesis_quorum", Separator = ',', HelpText = "Public keys of all auditors in genesis quorum, delimited with coma.")]
        public IEnumerable<string> GenesisQuorum { get; set; }

        public KeyPair AlphaKeyPair { get; set; }

        public override void Build()
        {
            base.Build();
            AlphaKeyPair = KeyPair.FromAccountId(AlphaPubKey);
        }
    }

    [Verb("alpha", HelpText = "Launch Centaurus in alpha mode.")]
    public class AlphaSettings : BaseSettings
    {
        [Option("alpha_port", Required = true, HelpText = "Port the alpha will listen on.")]
        public int AlphaPort { get; set; }

        [Option("alpha_cert", Required = false, HelpText = "Certificate path.")]
        public string TlsCertificatePath { get; set; }

        [Option("alpha_cert_pk", Required = false, HelpText = "Certificate private key file path.")]
        public string TlsCertificatePrivateKeyPath { get; set; }

        [Option("sync_batch_size", Default = 500, HelpText = "Max quanta sync batch size.")]
        public int SyncBatchSize { get; set; }
    }
}
