﻿using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class MessageEnvelope
    {
        [XdrField(0)]
        public Message Message { get; set; }

        [XdrField(1)]
        public List<Ed25519Signature> Signatures { get; set; }
    }
}