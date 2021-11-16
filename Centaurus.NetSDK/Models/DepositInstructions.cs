using System;

namespace Centaurus.NetSDK
{
    public class DepositInstructions
    {
        internal DepositInstructions(string depositAddress, string centaurusAccountAddress, string token)
        {
            if (string.IsNullOrWhiteSpace(depositAddress))
                throw new ArgumentNullException(nameof(depositAddress));
            if (string.IsNullOrWhiteSpace(centaurusAccountAddress))
                throw new ArgumentNullException(nameof(centaurusAccountAddress));
            DepositAddress = depositAddress;
            CentaurusAccountAddress = centaurusAccountAddress;
            Token = token;
        }

        /// <summary>
        /// The address on the blockchain network where funds should be deposited.
        /// </summary>
        public string DepositAddress { get; }

        /// <summary>
        /// Centaurus identifier data to be included into the transaction (using Stellar transaction memo, Ethereum data, etc.)
        /// </summary>
        public string CentaurusAccountAddress { get; }

        /// <summary>
        /// The blockchain's token. Could be null for native asset.
        /// </summary>
        public string Token { get; }
    }
}
