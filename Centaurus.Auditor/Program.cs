using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Centaurus.Auditor
{
    class Program
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {

            var merged = CommandLineHelper.GetMergedArgs<AuditorSettings>(args);
            Parser.Default.ParseArguments<AuditorSettings>(merged)
                .WithParsed(s => //settings parsed with no errors
                {
                    s.Build();
                    var startup = new Startup(s);
                    _ = startup.Run();

                    Console.WriteLine("Press Enter to quit");
                    Console.ReadLine();

                    startup.Abort().Wait();
                })
                .WithNotParsed(e => logger.Error(e));
        }
    }
}
