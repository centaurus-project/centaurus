using Centaurus.Alpha;
using Centaurus.Client;
using Centaurus.Domain;
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

            var parser = new Parser(s => { 
                s.AllowMultiInstance = true;
                s.AutoHelp = true;
                s.AutoVersion = true;
                s.HelpWriter = Parser.Default.Settings.HelpWriter;
            });
            var errors = parser.ParseArguments<Settings>(mergedArgs)
                .WithParsed(s => ConfigureAndRun(s));
        }

        static void ConfigureAndRun<T>(T settings)
            where T : Settings
        {
            var isLoggerInited = false;
            try
            {

                settings.Build();

                Console.Title = $"CentaurusAuditor_{settings.KeyPair.AccountId}";

                var logsDirectory = Path.Combine(settings.CWD, "logs");
                LogConfigureHelper.Configure(logsDirectory, settings.Silent, settings.Verbose);
                isLoggerInited = true;

                var context = new Domain.ExecutionContext(settings, new PersistentStorageAbstraction(), PaymentProvidersFactoryBase.Default, OutgoingConnectionFactoryBase.Default);
                var startup = new Startup(context, AlphaHostFactoryBase.Default);

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
