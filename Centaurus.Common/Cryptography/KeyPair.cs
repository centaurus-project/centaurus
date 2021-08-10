using dotnetstandard_bip32;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus
{
    public class KeyPair : IEquatable<KeyPair>
    {
        private KeyPair(Key secretKey, byte[] seed)
        {
            _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
            _publicKey = secretKey.PublicKey;
            SeedBytes = seed ?? throw new ArgumentNullException(nameof(seed));
        }

        /// <summary>
        /// Creates a new Keypair object from public key.
        /// </summary>
        /// <param name="publicKey"></param>
        public KeyPair(byte[] publicKey)
            : this(publicKey, null, null)
        {
        }

        /// <summary>
        /// Creates a new Keypair instance from secret. This can either be secret key or secret seed depending on underlying public-key signature system. Currently Keypair only supports ed25519.
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="privateKey"></param>
        /// <param name="seed"></param>
        public KeyPair(byte[] publicKey, byte[] privateKey, byte[] seed)
        {
            _publicKey = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, publicKey, KeyBlobFormat.RawPublicKey);

            if (privateKey != null)
            {
                _secretKey = Key.Import(SignatureAlgorithm.Ed25519, privateKey, KeyBlobFormat.RawPrivateKey,
                    new KeyCreationParameters() { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            }
            else
            {
                _secretKey = null;
            }

            SeedBytes = seed;
        }

        /// <summary>
        /// This method used for mapping raw string key to KeyPair instance.
        /// </summary>
        /// <param name="source"></param>
        public KeyPair(string source)
        {
            if (StrKey.IsValidEd25519SecretSeed(source))
            {
                SeedBytes = StrKey.DecodeStellarSecretSeed(source);
                _secretKey = Key.Import(SignatureAlgorithm.Ed25519, SeedBytes, KeyBlobFormat.RawPrivateKey,
                    new KeyCreationParameters() { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

                _publicKey = _secretKey.PublicKey;
            }
            else if (StrKey.IsValidEd25519PublicKey(source))
            {
                byte[] publicKey = StrKey.DecodeStellarAccountId(source);
                _publicKey = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, publicKey, KeyBlobFormat.RawPublicKey);
            }
            else
                throw new ArgumentException("Invalid key format.");
        }

        private readonly Key _secretKey;
        private readonly NSec.Cryptography.PublicKey _publicKey;

        /// <summary>
        /// The public key.
        /// </summary>
        public byte[] PublicKey => _publicKey.Export(KeyBlobFormat.RawPublicKey);

        /// <summary>
        /// The private key.
        /// </summary>
        public byte[] PrivateKey => _secretKey.Export(KeyBlobFormat.RawPrivateKey);

        /// <summary>
        /// The bytes of the Secret Seed
        /// </summary>
        public byte[] SeedBytes { get; }

        /// <summary>
        /// AccountId
        /// </summary>
        public string AccountId => StrKey.EncodeStellarAccountId(PublicKey);

        /// <summary>
        /// Address
        /// </summary>
        public string Address => StrKey.EncodeCheck(StrKey.VersionByte.ACCOUNT_ID, PublicKey);

        /// <summary>
        /// SecretSeed
        /// </summary>
        public string SecretSeed => StrKey.EncodeStellarSecretSeed(SeedBytes);

        /// <summary>
        /// The signing key.
        /// </summary>
        public KeyPair SigningKey => this;

        public bool IsMuxedAccount => false;

        /// <summary>
        ///     Returns true if this Keypair is capable of signing
        /// </summary>
        /// <returns></returns>
        public bool CanSign()
        {
            return _secretKey != null;
        }

        /// <summary>
        ///     Creates a new Stellar KeyPair from a strkey encoded Stellar secret seed.
        /// </summary>
        /// <param name="seed">eed Char array containing strkey encoded Stellar secret seed.</param>
        /// <returns>
        ///     <see cref="KeyPair" />
        /// </returns>
        public static KeyPair FromSecretSeed(string seed)
        {
            byte[] bytes = StrKey.DecodeStellarSecretSeed(seed);
            return FromSecretSeed(bytes);
        }

        /// <summary>
        ///     Creates a new Stellar keypair from a raw 32 byte secret seed.
        /// </summary>
        /// <param name="seed">seed The 32 byte secret seed.</param>
        /// <returns>
        ///     <see cref="KeyPair" />
        /// </returns>
        public static KeyPair FromSecretSeed(byte[] seed)
        {
            var privateKey = Key.Import(SignatureAlgorithm.Ed25519, seed, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters() { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            return new KeyPair(privateKey, seed);
        }

        /// <summary>
        ///     Creates a new Stellar KeyPair from a strkey encoded Stellar account ID.
        /// </summary>
        /// <param name="accountId">accountId The strkey encoded Stellar account ID.</param>
        /// <returns>
        ///     <see cref="KeyPair" />
        /// </returns>
        public static KeyPair FromAccountId(string accountId)
        {
            byte[] decoded = StrKey.DecodeStellarAccountId(accountId);
            return FromPublicKey(decoded);
        }

        public static bool TryGetFromAccountId(string accountId, out KeyPair keyPair)
        {
            keyPair = null;
            if (!StrKey.IsValidEd25519PublicKey(accountId))
                return false;
            byte[] decoded = StrKey.DecodeStellarAccountId(accountId);
            keyPair = FromPublicKey(decoded);
            return true;
        }

        public static KeyPair FromBIP39Seed(string seed, uint accountIndex)
        {
            BIP32 bip32 = new BIP32();

            string path = $"m/44'/148'/{accountIndex}'";
            return FromSecretSeed(bip32.DerivePath(path, seed).Key);
        }

        public static KeyPair FromBIP39Seed(byte[] seedBytes, uint accountIndex)
        {
            string seed = seedBytes.ToStringHex();
            return FromBIP39Seed(seed, accountIndex);
        }

        /// <summary>
        ///     Creates a new Stellar keypair from a 32 byte address.
        /// </summary>
        /// <param name="publicKey">publicKey The 32 byte public key.</param>
        /// <returns>
        ///     <see cref="KeyPair" />
        /// </returns>
        public static KeyPair FromPublicKey(byte[] publicKey)
        {
            return new KeyPair(publicKey);
        }

        /// <summary>
        ///     Generates a random Stellar keypair.
        /// </summary>
        /// <returns>a random Stellar keypair</returns>
        public static KeyPair Random()
        {
            byte[] b = new byte[32];
            using (RNGCryptoServiceProvider rngCrypto = new RNGCryptoServiceProvider())
            {
                rngCrypto.GetBytes(b);
            }

            return FromSecretSeed(b);
        }

        /// <summary>
        ///     Sign the provided data with the keypair's private key.
        /// </summary>
        /// <param name="data">The data to sign.</param>
        /// <returns>signed bytes, null if the private key for this keypair is null.</returns>
        public byte[] Sign(byte[] data)
        {
            if (_secretKey == null)
            {
                throw new Exception("KeyPair does not contain secret key. Use KeyPair.fromSecretSeed method to create a new KeyPair with a secret key.");
            }

            return SignatureAlgorithm.Ed25519.Sign(_secretKey, data);
        }

        /// <summary>
        ///     Verify the provided data and signature match this keypair's public key.
        /// </summary>
        /// <param name="data">The data that was signed.</param>
        /// <param name="signature">The signature.</param>
        /// <returns>True if they match, false otherwise.</returns>
        public bool Verify(byte[] data, byte[] signature)
        {
            try
            {
                return SignatureAlgorithm.Ed25519.Verify(_publicKey, data, signature);
            }
            catch
            {
                return false;
            }
        }

        public bool Equals(KeyPair other)
        {
            if (other == null) return false;
            //if (SeedBytes != null && other.SeedBytes == null) return false;
            //if (SeedBytes == null && other.SeedBytes != null) return false;
            return _publicKey.Equals(other._publicKey);
        }
    }
}
