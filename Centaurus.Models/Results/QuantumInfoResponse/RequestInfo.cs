using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion(0, typeof(RequestHashInfo))]
    [XdrUnion(1, typeof(RequestInfo))]
    public abstract class RequestInfoBase
    {
        [XdrField(0)]
        public byte[] Data { get; set; }
    }

    public class RequestHashInfo: RequestInfoBase
    {
    }

    public class RequestInfo : RequestInfoBase
    {
    }
}
