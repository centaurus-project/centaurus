﻿using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class QuantumPersistentModel : IPersistentModel, IPrefixedPersistentModel
    {
        /**
         * Quantum apex.
         */
        [IgnoreMember]
        public ulong Apex { get; set; }

        /**
         * Quantum application timestamp.
         */
        [Key(0)]
        public long TimeStamp { get; set; }

        /**
         * Quantum details in a raw binary serialized form.
         */
        [Key(1)]
        public byte[] RawQuantum { get; set; }

        [Key(2)]
        public List<AccountEffects> Effects { get; set; }

        [Key(3)]
        public List<SignatureModel> Signatures { get; set; }

        [IgnoreMember]
        public byte[] Key
        {
            get => ApexConverter.EncodeApex(Apex);
            set => Apex = ApexConverter.DecodeApex(value);
        }

        [IgnoreMember]
        public string ColumnFamily => "quanta";

        [IgnoreMember]
        public uint PrefixLength => 8;
    }

    [MessagePackObject]
    public class SignatureModel
    {
        [Key(0)]
        public byte[] PayloadSignature { get; set; }

        [Key(1)]
        public byte[] TxSigner { get; set; }

        [Key(2)]
        public byte[] TxSignature { get; set; }
    }

    [MessagePackObject]
    public class AccountEffects
    {
        [Key(0)]
        public ulong Account { get; set; }

        [Key(1)]
        public byte[] Effects { get; set; }
    }
}
