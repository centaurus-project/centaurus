using Centaurus.NetSDK;
using System;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockClientConstellationConfig : ConstellationConfig
    {
        private NetSDK.ConstellationInfo constellationInfo;

        public MockClientConstellationConfig(string alphaServerAddress, byte[] secretKey, MockOutgoingConnectionFactory mockOutgoingConnection, NetSDK.ConstellationInfo constellationInfo, bool useSecureConnection = true)
            :base(alphaServerAddress, secretKey, useSecureConnection, mockOutgoingConnection)
        {
            this.constellationInfo = constellationInfo ?? throw new ArgumentNullException(nameof(constellationInfo));
        }

        public override Task<NetSDK.ConstellationInfo> GetConstellationInfo()
        {
            return Task.FromResult(constellationInfo);
        }
    }
}
