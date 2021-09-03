using Centaurus.NetSDK;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockClientConstellationConfig : ConstellationConfig
    {
        public MockClientConstellationConfig(string alphaServerAddress, byte[] secretKey, MockOutgoingConnectionFactory mockOutgoingConnection, NetSDK.ConstellationInfo constellationInfo, bool useSecureConnection = true)
            :base(alphaServerAddress, secretKey, useSecureConnection)
        {
            //mock connection factory
            var outgoingConnectionFactoryField = typeof(ConstellationConfig).GetField($"<OutgoingConnectionFactory>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            outgoingConnectionFactoryField.SetValue(this, mockOutgoingConnection);

            //mock constellation info request
            Func<string, bool, Task<NetSDK.ConstellationInfo>> getConstellationInfo = (address, ishttps) => Task.FromResult(constellationInfo);
            var getConstellationInfoField = typeof(ConstellationConfig).GetField($"<GetConstellationInfo>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            getConstellationInfoField.SetValue(this, getConstellationInfo);
        }
    }
}
