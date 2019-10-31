namespace Centaurus.Models
{
    public class SetApexCursor : Message
    {
        public override MessageTypes MessageType => MessageTypes.SetApexCursor;

        public ulong Apex { get; set; }

        public void Deserialize(ref SetApexCursor value, XdrReader reader)
        {
            value.Apex = reader.ReadUInt64();
        }

        public void Serialize(SetApexCursor value, XdrWriter writer)
        {
            writer.WriteUInt64(value.Apex);
        }
    }
}
