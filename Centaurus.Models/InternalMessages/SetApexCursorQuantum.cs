namespace Centaurus.Models
{
    public class SetApexCursor : Message
    {
        public override MessageTypes MessageType => MessageTypes.SetApexCursor;
        
        [XdrField(0)]
        public long Apex { get; set; }
    }
}
