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

                    ConfigureConstellation(s).Wait();

                    host = CreateHostBuilder(args).Build();
                    host.Run();
                })
                .WithNotParsed(e => logger.Error(e));
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

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var urls = new List<string>();
                    config.GetSection("AppUrls").Bind(urls);

                    if (urls.Count < 1)
                        throw new Exception("No application urls were specified");

                    webBuilder.UseStartup<Startup>()
                        .UseUrls(urls.ToArray());
                });
        }

        static async Task Shutdown()
        {
            await host.StopAsync();
        }

    }
}
