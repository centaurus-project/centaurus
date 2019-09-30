using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Centaurus.Test
{
    public static class PerfCounter
    {
        public static void MeasureTime(Action action, Func<string> output = null, [CallerMemberName] string caller = null)
        {
            var st = new Stopwatch();
            st.Start();
            action();
            st.Stop();
            TestContext.Out.WriteLine($"[{caller}] - {output?.Invoke()??"finished"} - {st.ElapsedMilliseconds} ms");
        }
    }
}
