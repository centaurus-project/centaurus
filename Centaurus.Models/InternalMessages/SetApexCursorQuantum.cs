namespace Centaurus.Models
{
    public class SetApexCursor : Message
    {
        public override MessageTypes MessageType => MessageTypes.SetApexCursor;

        public ulong Apex { get; set; }
    }
}
