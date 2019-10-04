using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;

namespace Centaurus
{
    public class Program
    {
        private static IHost host;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
        {
            var mergedArgs = CommandLineHelper.GetMergedArgs<AlphaSettings>(args);

            Parser.Default.ParseArguments<AlphaSettings>(mergedArgs)
                .WithParsed(s =>
                {
                    s.Build();

                    var logsDirectory = Path.Combine(s.CWD, "logs");
                    LogConfigureHelper.Configure(logsDirectory, s.Silent, s.Verbose);

                    ConfigureConstellation(s).Wait();

                    host = CreateHostBuilder(s).Build();
                    host.Run();
                })
                .WithNotParsed(e => {
                    LogConfigureHelper.Configure("logs");
                    logger.Error(e);

                    Console.WriteLine("Press Enter to exit");
                    Console.ReadLine();
                });
        }

        private static async Task ConfigureConstellation(AlphaSettings settings)
        {
            //force serializers load
            _ = new SnapshotSerializer();

            Global.Init(settings);

            Global.AppState.StateChanged += Current_StateChanged;

            MessageHandlers<AlphaWebSocketConnection>.Init();

            var lastSnapshot = await SnapshotProviderManager.GetLastSnapshot();

            if (lastSnapshot == null)
            {
                Global.AppState.State = ApplicationState.WaitingForInit;
            }
            else
            {
                Global.Setup(lastSnapshot);
                Global.AppState.State = ApplicationState.Rising;
            }
        }

        private static async void Current_StateChanged(object sender, ApplicationState e)
        {
            if (e == ApplicationState.Failed)
            {
                logger.Error("Application failed");

                ConnectionManager.CloseAllConnections();

                await Shutdown();
            }
        }

        public static IHostBuilder CreateHostBuilder(AlphaSettings settings)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => {

                    logging.ClearProviders();
                    logging.AddConsole();

                    if (settings.Verbose)
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    else if (settings.Silent)
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Error);
                    else
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                })
                .UseNLog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .UseUrls(settings.AlphaUrl);
                });
        }

        static async Task Shutdown()
        {
            await host.StopAsync();
        }

    }
}
