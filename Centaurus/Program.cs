using System;
using System.Diagnostics;
using System.IO;
using CommandLine;
using NLog;

namespace Centaurus
{
    public class Program
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
        {
            var mergedArgs = CommandLineHelper.GetMergedArgs<AuditorSettings>(CommandLineHelper.GetMergedArgs<AlphaSettings>(args));

            Parser.Default.ParseArguments<AlphaSettings, AuditorSettings>(mergedArgs)
                .WithParsed<AlphaSettings>(s => ConfigureAndRun(s))
                .WithParsed<AuditorSettings>(s => ConfigureAndRun(s))
                .WithNotParsed(e =>
                {
                    Console.WriteLine("Press Enter to quit");
                    Console.ReadLine();
                });
        }

        static void ConfigureAndRun<T>(T settings)
            where T : BaseSettings
        {
            var isAlpha = settings is AlphaSettings;
            Console.Title = isAlpha ? "CentaurusAlpha" : "CentaurusAuditor";

            settings.Build();

            var logsDirectory = Path.Combine(settings.CWD, "logs");
            LogConfigureHelper.Configure(logsDirectory, settings.Silent, settings.Verbose);

            var startup = isAlpha ? (IStartup<T>)new AlphaStartup() : (IStartup<T>)new AuditorStartup();

            startup.Run(settings);

            if (!isAlpha)
                logger.Info("Auditor is started");

            Console.WriteLine("Press Enter to quit");
            Console.ReadLine();

            startup.Shutdown();
        }

    }
}
