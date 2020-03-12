using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class RequestRateLimitsModel
    {
        public int HourLimit { get; set; }
        public int MinuteLimit { get; set; }
    }
}
