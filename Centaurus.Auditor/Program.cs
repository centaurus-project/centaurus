using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
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

                    var logsDirectory = Path.Combine(s.CWD, "logs");
                    LogConfigureHelper.Configure(logsDirectory, s.Silent, s.Verbose);

                    var startup = new Startup(s);
                    _ = startup.Run();

                    logger.Info("Auditor is started");

                    Console.WriteLine("Press Enter to quit");
                    Console.ReadLine();

                    startup.Abort().Wait();
                })
                .WithNotParsed(e =>
                {
                    LogConfigureHelper.Configure("logs");
                    logger.Error(e);

                    Console.WriteLine("Press Enter to exit");
                    Console.ReadLine();
                });
        }
    }
}
