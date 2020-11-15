using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Analytics
{
    public enum OHLCFramePeriod
    {
        Minute = 60,
        Minutes15 = 900,
        Minutes30 = 1_800,
        Hour = 3_600,
        Hours4 = 14_400,
        Day = 86_400,
        Week = 604_800,
        Month = 2_592_000
    }
}