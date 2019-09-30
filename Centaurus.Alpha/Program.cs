using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Centaurus
{
    public class Program
    {
        private static IHost host;

        public static void Main(string[] args)
        {
            host = CreateHostBuilder(args).Build();
            host.Run();
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

        public static async Task Shutdown()
        {
            await host.StopAsync();
        }

    }
}
