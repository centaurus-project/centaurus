using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseResponse
    {
        public long RequestId { get; set; }

        public int Status { get; set; } = 200;
    }
}
