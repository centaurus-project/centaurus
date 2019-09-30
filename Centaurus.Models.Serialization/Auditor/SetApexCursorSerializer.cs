namespace Centaurus.Models
{
    public class SetApexCursorSerializer : IXdrSerializer<SetApexCursor>
    {
        public void Deserialize(ref SetApexCursor value, XdrReader reader)
        {
            value.Apex = reader.ReadUInt64();
        }

        public void Serialize(SetApexCursor value, XdrWriter writer)
        {
            writer.Write(value.Apex);
        }
    }
}
