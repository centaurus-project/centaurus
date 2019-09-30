using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationSettingsSerializer : IXdrSerializer<ConstellationSettings>
    {

        public void Deserialize(ref ConstellationSettings value, XdrReader reader)
        {
            value.Vault = reader.Read<RawPubKey>();
            value.Auditors = reader.ReadList<RawPubKey>();
            value.MinAccountBalance = reader.ReadInt64();
            value.MinAllowedLotSize = reader.ReadInt64();
            value.Assets = reader.ReadList<AssetSettings>();
        }

        public void Serialize(ConstellationSettings value, XdrWriter writer)
        {
            writer.Write(value.Vault);
            writer.Write(value.Auditors);
            writer.Write(value.MinAccountBalance);
            writer.Write(value.MinAllowedLotSize);
            writer.Write(value.Assets);
        }
    }
}
