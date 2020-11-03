using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Analytics
{
    public enum OHLCFramePeriod
    {
        Minute = 60,
        Hour = 3_600,
        Day = 86_400,
        Week = 604_800,
        Month = 2_592_000
    }
}