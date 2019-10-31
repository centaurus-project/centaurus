namespace Centaurus.Models
{
    public class SetApexCursor : Message
    {
        public override MessageTypes MessageType => MessageTypes.SetApexCursor;
        
        [XdrField(0)]
        public ulong Apex { get; set; }
    }
}
