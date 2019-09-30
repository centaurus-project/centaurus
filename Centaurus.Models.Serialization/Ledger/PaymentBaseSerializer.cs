using stellar_dotnet_sdk.xdr;
using System;

namespace Centaurus.Models
{
    public class PaymentBaseSerializer : IXdrSerializer<PaymentBase>
    {
        private PaymentBase CreateInstance(XdrReader reader)
        {
            var type = reader.ReadEnum<PaymentTypes>();
            switch (type)
            {
                case PaymentTypes.Deposit: return new Deposit();
                case PaymentTypes.Withdrawal: return new Withdrawal();
            }
            throw new InvalidOperationException("Unsupported payment type: " + type.ToString());
        }

        public void Deserialize(ref PaymentBase value, XdrReader reader)
        {
            value = CreateInstance(reader);
            value.Asset = reader.ReadInt32();
            value.Amount = reader.ReadInt64();
            value.TransactionHash = reader.ReadVariable();
            value.Destination = reader.Read<RawPubKey>();
            value.PaymentResult = reader.ReadEnum<PaymentResults>();
        }

        public void Serialize(PaymentBase value, XdrWriter writer)
        {
            writer.Write(value.Type);
            writer.Write(value.Asset);
            writer.Write(value.Amount);
            writer.Write(value.TransactionHash);
            writer.Write(value.Destination);
            writer.Write(value.PaymentResult);
        }
    }
}
