using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus
{
    public class Settings
    {
        public KeyPair KeyPair { get; private set; }

        public KeyPair AlphaKeyPair { get; private set; }

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

        [Option("alpha_pubkey", Required = true, HelpText = "Alpha server public key.")]
        public string AlphaPubKey { get; set; }

        [Option("alpha_port", Required = true, HelpText = "Port the alpha will listen on.")]
        public int AlphaPort { get; set; }

        [Option("alpha_cert", Required = false, HelpText = "Certificate path.")]
        public string TlsCertificatePath { get; set; }

        [Option("alpha_cert_pk", Required = false, HelpText = "Certificate private key file path.")]
        public string TlsCertificatePrivateKeyPath { get; set; }

        [Option("sync_batch_size", Default = 500, HelpText = "Max quanta sync batch size.")]
        public int SyncBatchSize { get; set; }

        [Option("participation_level", Default = 0, HelpText = "Centaurus node participation level.")]
        public int ParticipationLevel { get; set; }

        [Option("payment_config", Required = false, HelpText = "Payment providers config path.")]
        public string PaymentConfigPath { get; set; }

        public void Build()
        {
            KeyPair = KeyPair.FromSecretSeed(Secret);
            AlphaKeyPair = KeyPair.FromAccountId(AlphaPubKey);
        }
    }
}
