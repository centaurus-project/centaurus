using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace Centaurus
{
    public static class TimerHelper
    {
        public static void Reset(this Timer timer)
        {
            timer.Stop();
            timer.Start();
        }
    }
}
