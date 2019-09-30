using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class MessageSerializer : IXdrSerializer<Message>
    {
        private Message ReadMessage(XdrReader reader)
        {
            var type = reader.ReadEnum<MessageTypes>();
            switch (type)
            { //TODO: implement the delegated deserialization for quanta and results
                case MessageTypes.OrderRequest:
                    return reader.Read<OrderRequest>();
                case MessageTypes.PaymentRequest:
                    return reader.Read<PaymentRequest>();
                case MessageTypes.WithdrawalRequest:
                    return reader.Read<WithdrawalRequest>();
                case MessageTypes.HandshakeInit:
                    return reader.Read<HandshakeInit>();
                case MessageTypes.ResultMessage:
                    return reader.Read<ResultMessage>();
                case MessageTypes.SetApexCursor:
                    return reader.Read<SetApexCursor>();
                case MessageTypes.Heartbeat:
                    return reader.Read<Heartbeat>();
                case MessageTypes.SnapshotQuantum:
                    return reader.Read<SnapshotQuantum>();
                case MessageTypes.LedgerCommitQuantum:
                    return reader.Read<LedgerCommitQuantum>();
                case MessageTypes.LedgerUpdateNotification:
                    return reader.Read<LedgerUpdateNotification>();
                case MessageTypes.AuditorState:
                    return reader.Read<AuditorState>();
                case MessageTypes.AlphaState:
                    return reader.Read<AlphaState>();
                case MessageTypes.RequestQuantum:
                    return reader.Read<RequestQuantum>();
            }
            throw new NotImplementedException("Unsupported message type " + type.ToString());
        }

        public void Deserialize(ref Message value, XdrReader reader)
        {
            if (value == null)
            {
                value = ReadMessage(reader);
            }
        }

        public void Serialize(Message value, XdrWriter writer)
        {
            writer.Write((value as Message).MessageType);
        }
    }
}
