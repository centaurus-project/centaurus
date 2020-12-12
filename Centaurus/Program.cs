using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
                .WithParsed<AuditorSettings>(s => ConfigureAndRun(s));
        }

        static void ConfigureAndRun<T>(T settings)
            where T : BaseSettings
        {
            var isLoggerInited = false;
            try
            {
                var isAlpha = settings is AlphaSettings;
                Console.Title = isAlpha ? "CentaurusAlpha" : "CentaurusAuditor";

                settings.Build();

                var logsDirectory = Path.Combine(settings.CWD, "logs");
                LogConfigureHelper.Configure(logsDirectory, settings.Silent, settings.Verbose);
                isLoggerInited = true;

                var startup = isAlpha ? (IStartup<T>)new AlphaStartup() : (IStartup<T>)new AuditorStartup();

                var resetEvent = new ManualResetEvent(false);
                startup.Run(settings, resetEvent);

                if (!isAlpha)
                    logger.Info("Auditor is started");
                Console.WriteLine("Press Ctrl+C to quit");
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    // Cancel the cancellation to allow the program to shutdown cleanly.
                    eventArgs.Cancel = true;
                    resetEvent.Set();
                };
                resetEvent.WaitOne();
                startup.Shutdown();
            }
            catch (Exception exc)
            {
                if (!isLoggerInited)
                    LogConfigureHelper.Configure("logs");
                logger.Error(exc);
                Thread.Sleep(5000);
            }
        }

    }
}
