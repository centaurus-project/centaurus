using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class RequestRateLimitsModel
    {
        public uint HourLimit { get; set; }
        public uint MinuteLimit { get; set; }
    }
}
