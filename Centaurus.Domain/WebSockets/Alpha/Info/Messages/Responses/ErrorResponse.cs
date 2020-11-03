using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class ErrorResponse : BaseResponse
    {
        public ErrorResponse()
        {
            Status = 500;
        }

        public string Error { get; set; }
    }
}