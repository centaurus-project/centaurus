using System.Text;

namespace Centaurus.Models
{
    public class HandshakeInitSerializer : IXdrSerializer<HandshakeInit>
    {
        public void Deserialize(ref HandshakeInit value, XdrReader reader)
        {
            value.HandshakeData = reader.Read<HandshakeData>();
        }

        public void Serialize(HandshakeInit value, XdrWriter writer)
        {
            writer.Write(value.HandshakeData);
        }
    }
}
