using System;
using System.Buffers.Binary;
using MessagePack;

namespace Centaurus.PersistentStorage
{
    /// <summary>
    /// Reference between an account and quantum apexes that contain account effects.
    /// </summary>
    [MessagePackObject]
    public class QuantumRefPersistentModel : IPersistentModel, IPrefixedPersistentModel
    {
        /**
         * Public key of an account to which an effect has been applied.
         */
        [IgnoreMember]
        public ulong AccountId { get; set; }

        /**
         * Quantum apex.
         */
        [IgnoreMember]
        public ulong Apex { get; set; }

        /**
         * Is the quantum was initiated by the account.
         */
        [Key(0)]
        public bool IsQuantumInitiator { get; set; }

        [IgnoreMember]
        public byte[] Key
        {
            get
            {
                //{account_pubkey}{quantum_apex}
                var rawId = new byte[16];
                BinaryPrimitives.WriteUInt64BigEndian(rawId.AsSpan(0, 8), AccountId);
                BinaryPrimitives.WriteUInt64BigEndian(rawId.AsSpan(8, 8), Apex);
                return rawId;
            }
            set
            {
                AccountId = BinaryPrimitives.ReadUInt64BigEndian(value.AsSpan(0, 8));
                Apex = BinaryPrimitives.ReadUInt64BigEndian(value.AsSpan(8, 8));
            }
        }

        [IgnoreMember]
        public string ColumnFamily => "quantumref";

        [IgnoreMember]
        public uint PrefixLength => 32;
    }
}
