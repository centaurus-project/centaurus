using NLog;
using System;
using System.Threading;

namespace Centaurus.Auditor
{
    class Program
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            var startup = new Startup();
            _ = startup.Run();

            Console.ReadLine();

            startup.Abort().Wait();
        }
    }
}
