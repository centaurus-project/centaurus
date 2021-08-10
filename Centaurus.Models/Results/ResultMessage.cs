using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class ResultMessage : ResultMessageBase
    {
        public override MessageTypes MessageType => MessageTypes.ResultMessage;
    }
}
