using Newtonsoft.Json;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Centaurus
{

    public class StellarNetworkSettings
    {
        public string Passphrase { get; set; }
        public string Horizon { get; set; }
    }

    public class AppSettings
    {
        public static AppSettings Load(string settingsFilePath = "appsettings.json")
        {
            using (StreamReader r = new StreamReader(settingsFilePath))
            {
                string json = r.ReadToEnd();
                AppSettings config = JsonConvert.DeserializeObject<AppSettings>(json);

                config.KeyPair = KeyPair.FromSecretSeed(config.Secret);
                if (config.IsAlpha)
                    config.AlphaKeyPair = config.KeyPair;
                else
                    config.AlphaKeyPair = KeyPair.FromAccountId(config.AlphaPubKey);

                //TODO: check that all required properties are set
                return config;
            }
        }

        public KeyPair KeyPair { get; set; }

        public string Secret { get; set; }

        public string AlphaAddress { get; set; }

        public string AlphaPubKey { get; set; }

        public KeyPair AlphaKeyPair { get; set; }

        public bool IsAlpha { get; set; }

        public string SnapshotsDirectory { get; set; }

        public string[] DefaultAuditors { get; set; } = new string[] { };

        public bool IsAuditor
        {
            get
            {
                return !IsAlpha;
            }
        }

        public StellarNetworkSettings StellarNetwork { get; set; }
    }
}
