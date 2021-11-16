using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class PayloadProof
    {
        /// <summary>
        /// Hash of concatenated apex bytes, quantum hash and effects hash
        /// </summary>
        [XdrField(0)]
        public byte[] PayloadHash { get; set; }

        /// <summary>
        /// PayloadHash signatures
        /// </summary>
        [XdrField(1)]
        public List<TinySignature> Signatures { get; set; }
    }
}