using Centaurus.Alpha;
using Centaurus.Client;
using Centaurus.PersistentStorage.Abstraction;
using CommandLine;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Centaurus
{
    public class Program
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
        {
            var mergedArgs = CommandLineHelper.GetMergedArgs<Settings>(args);

            Parser.Default.ParseArguments<Settings>(mergedArgs)
                .WithParsed(s => ConfigureAndRun(s));
        }

        static void ConfigureAndRun<T>(T settings)
            where T : Settings
        {
            var isLoggerInited = false;
            try
            {
                settings.Build();

                var isAlpha = settings.KeyPair.AccountId == settings.AlphaKeyPair.AccountId;
                Console.Title = isAlpha ? "CentaurusAlpha" : "CentaurusAuditor";

                settings.Build();

                var logsDirectory = Path.Combine(settings.CWD, "logs");
                LogConfigureHelper.Configure(logsDirectory, settings.Silent, settings.Verbose);
                isLoggerInited = true;

                var context = new Domain.ExecutionContext(settings, new PersistentStorageAbstraction(), PaymentProvider.PaymentProviderFactoryBase.Default);
                var startup = new StartupMain(context, ClientConnectionFactoryBase.Default, AlphaHostFactoryBase.Default);

                var resetEvent = new ManualResetEvent(false);
                startup.Run(resetEvent);

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
