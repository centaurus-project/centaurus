using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AssetSettingsSerializer : IXdrSerializer<AssetSettings>
    {
        public void Deserialize(ref AssetSettings value, XdrReader reader)
        {
            value.Id = reader.ReadInt32();
            value.Code = reader.ReadString();
            value.Issuer = reader.ReadOptional<RawPubKey>();
        }

        public void Serialize(AssetSettings value, XdrWriter writer)
        {
            writer.Write(value.Id);
            writer.Write(value.Code);
            writer.WriteOptional(value.Issuer);
        }
    }
}
