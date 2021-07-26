namespace Centaurus.NetSDK
{
    public class DepositInstructions
    {
        internal DepositInstructions(string depositAddress, byte[] centaurusAccountAddress)
        {
            DepositAddress = depositAddress;
            CentaurusAccountAddress = centaurusAccountAddress;
        }

        /// <summary>
        /// The address on the blockchain network where funds should be deposited.
        /// </summary>
        public readonly string DepositAddress;

        /// <summary>
        /// Centaurus identifier data to be included into the transaction (using Stellar transaction memo, Ethereum data, etc.)
        /// </summary>
        public readonly byte[] CentaurusAccountAddress;
    }
}
