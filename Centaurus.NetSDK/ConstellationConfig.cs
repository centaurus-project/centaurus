﻿using Centaurus.Client;
using System.Threading.Tasks;

namespace Centaurus.NetSDK
{
    /// <summary>
    /// Basic constellation config containing information about the connection.
    /// </summary>
    public class ConstellationConfig
    {
        /// <summary>
        /// Create constellation config object.
        /// </summary>
        /// <param name="alphaServerAddress">Fully-qualified domain name of the Alpha server.</param>
        /// <param name="secretKey">Secret key used to sign messages on behalf of the account.</param>
        /// <param name="useSecureConnection">Whether to use TLS encryption for the connection (recommended for production usage).</param>
        public ConstellationConfig(string alphaServerAddress, byte[] secretKey, bool useSecureConnection = true)
            :this(alphaServerAddress, secretKey, useSecureConnection, null)
        {

        }

        protected ConstellationConfig(string alphaServerAddress, byte[] secretKey, bool useSecureConnection = true, OutgoingConnectionFactoryBase outgoingConnectionFactoryBase = null)
        {
            AlphaServerAddress = alphaServerAddress;
            ClientKeyPair = KeyPair.FromSecretSeed(secretKey);
            UseSecureConnection = useSecureConnection;
            OutgoingConnectionFactory = outgoingConnectionFactoryBase ?? OutgoingConnectionFactoryBase.Default;
        }

        /// <summary>
        /// Fully-qualified domain name of the Alpha server.
        /// </summary>
        public readonly string AlphaServerAddress;

        /// <summary>
        /// Secret key used to sign messages on behalf of the account.
        /// </summary>
        public readonly KeyPair ClientKeyPair;

        /// <summary>
        /// Whether to use Finalization or Acknowledge for state update.
        /// </summary>
        public readonly bool IsMajorityRequired;

        /// <summary>
        /// Whether to use TLS encryption for the connection.
        /// </summary>
        public readonly bool UseSecureConnection;

        internal OutgoingConnectionFactoryBase OutgoingConnectionFactory { get; }

        public virtual async Task<ConstellationInfo> GetConstellationInfo()
        {
            return await PublicApi.GetConstellationInfo(this);
        }
    }
}