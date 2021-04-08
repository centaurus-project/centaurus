using System;
using System.Net.WebSockets;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.DAL.Mongo;
using Microsoft.Extensions.Logging;
using NLog.Web;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Centaurus
{
    public class AlphaStartup : StartupBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private IHost host;
        private ManualResetEvent resetEvent;

        public AlphaStartup(AlphaContext context)
            : base(context)
        {
        }

        public override async Task Run(ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;

                host = CreateHostBuilder((AlphaSettings)Context.Settings).Build();
                _ = host.RunAsync();
                await ConfigureConstellation();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                if (Context.AppState != null)
                    Context.AppState.State = ApplicationState.Failed;
                resetEvent.Set();
            }
        }

        public override async Task Shutdown()
        {
            if (host != null)
            {
                var alphaContext = (AlphaContext)Context;
                await alphaContext.ConnectionManager.CloseAllConnections();
                await alphaContext.InfoConnectionManager.CloseAllConnections();
                await Context.TearDown();
                await host.StopAsync(CancellationToken.None);
                host = null;
            }
        }

        #region Private Members

        private async Task ConfigureConstellation()
        {
            await Context.Init();

            Context.AppState.StateChanged += Current_StateChanged;

            MessageHandlers<AlphaWebSocketConnection>.Init(Context);
        }

        private void Current_StateChanged(StateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == ApplicationState.Failed)
            {
                Console.WriteLine("Application failed. Saving pending updates...");
                Thread.Sleep(PendingUpdatesManager.SaveInterval);
                resetEvent.Set();
            }
        }

        private void SetupCertificate(AlphaSettings alphaSettings)
        {
            if (alphaSettings.Certificate == null)
                return;

            var certData = alphaSettings.Certificate.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (certData.Length < 1 || certData.Length > 2)
                throw new Exception("Invalid certificate settings data.");

            var certPath = certData[0];
            var pkPath = certData.Length == 1 ? null : certData[1];
            if (!File.Exists(certPath))
                throw new FileNotFoundException($"Failed to find a certificate \"{certPath}\"");

            UpdateCertificate(certPath, pkPath);

            ObserveCertificateChange(certPath, pkPath);
        }

        private void ObserveCertificateChange(string certPath, string pkPath)
        {
            if (string.IsNullOrWhiteSpace(certPath))
                throw new ArgumentNullException(nameof(certPath));

            var certFolder = Path.GetDirectoryName(certPath);
            var certFileName = Path.GetFileName(certPath);

            var watcher = new FileSystemWatcher(certFolder, certFileName);

            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += (s, e) =>
            {
                try
                {
                    lock (syncRoot)
                        UpdateCertificate(certPath, pkPath);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error on certificate update");
                }
            };
            watcher.Error += (s, eArgs) => logger.Error(eArgs.GetException());
            watcher.EnableRaisingEvents = true;
        }

        private void UpdateCertificate(string certFile, string privateKeyFile)
        {
            certificate = CertificateExtensions.GetSertificate(certFile, privateKeyFile);
        }

        private object syncRoot = new { };
        private X509Certificate2 certificate;
        private X509Certificate2 Certificate
        {
            get
            {
                lock (syncRoot)
                    return certificate;
            }
        }

        private IHostBuilder CreateHostBuilder(AlphaSettings settings)
        {
            SetupCertificate(settings);
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, configBuilder) =>
                {
                    if (string.IsNullOrEmpty(settings.AllowedHosts))
                        return;
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["AllowedHosts"] = settings.AllowedHosts
                    });
                })
                .ConfigureLogging(logging =>
                {

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
                    webBuilder.ConfigureServices(s => s.AddSingleton(Context))
                        .UseStartup<Startup>()
                        .UseKestrel(options =>
                        {
                            foreach (var endpoint in settings.AlphaEndpoints)
                            {
                                var endpointData = GetEndointData(endpoint);
                                if (endpointData.isHttps)
                                {
                                    if (certificate == null)
                                        throw new Exception("Unable to load certificate.");
                                    options.ListenAnyIP(endpointData.port,
                                    listenOptions =>
                                    {
                                        var httpsOptions = new HttpsConnectionAdapterOptions();
                                        httpsOptions.ServerCertificateSelector = (context, path) => Certificate;
                                        listenOptions.UseHttps(httpsOptions);
                                    });
                                }
                                else
                                    options.ListenAnyIP(endpointData.port);

                                logger.Trace($"Added listening. Raw endpoint: {endpoint}. Is https: {endpointData.isHttps}; port: {endpointData.port}.");
                            }
                        });
                });
        }

        private static (bool isHttps, int port) GetEndointData(string rawData)
        {
            var regex = new Regex(@"(https?):?(\d*)\/?(.*)", RegexOptions.IgnoreCase);

            var match = regex.Match(rawData);

            var isHttps = match.Groups["1"].Value.Equals("https", StringComparison.OrdinalIgnoreCase);
            var portStr = match.Groups["2"].Value;
            if (!int.TryParse(portStr, out var port))
                port = isHttps ? 443 : 80;
            return (isHttps, port);
        }
    }

    class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc(options => options.EnableEndpointRouting = false);
            //.AddJsonOptions(options =>
            //{
            //    options.JsonSerializerOptions.Converters.Add(new AssetSettingsConverter());
            //});
            services.Add(
                new ServiceDescriptor(
                    typeof(IActionResultExecutor<JsonResult>),
                    Type.GetType("Microsoft.AspNetCore.Mvc.Infrastructure.SystemTextJsonResultExecutor, Microsoft.AspNetCore.Mvc.Core"),
                    ServiceLifetime.Singleton)
            );
            services.AddOptions<HostOptions>().Configure(opts => opts.ShutdownTimeout = TimeSpan.FromDays(365));
        }

        static ApplicationState[] ValidApplicationStates = new ApplicationState[] { ApplicationState.Rising, ApplicationState.Running, ApplicationState.Ready };

        const string centaurusWebSocketEndPoint = "/centaurus";
        const string infoWebSocketEndPoint = "/info";

        static Dictionary<string, Func<HttpContext, Func<Task>, Task>> webSocketHandlers = new Dictionary<string, Func<HttpContext, Func<Task>, Task>>
            {
                { centaurusWebSocketEndPoint, CentaurusWebSocketHandler },
                { infoWebSocketEndPoint, InfoWebSocketHandler }
            };

        static async Task CentaurusWebSocketHandler(HttpContext context, Func<Task> next)
        {
            var centaurusContext = context.RequestServices.GetService<AlphaContext>();
            if (centaurusContext.AppState == null || ValidApplicationStates.Contains(centaurusContext.AppState.State))
            {
                using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
                    await centaurusContext.ConnectionManager.OnNewConnection(webSocket, context.Connection.RemoteIpAddress.ToString());
            }
            else
            {
                context.Abort();
            }
        }

        static async Task InfoWebSocketHandler(HttpContext context, Func<Task> next)
        {
            var centaurusContext = context.RequestServices.GetService<AlphaContext>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await centaurusContext.InfoConnectionManager.OnNewConnection(webSocket, context.Connection.Id, context.Connection.RemoteIpAddress.ToString());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHostFiltering();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                //app.UseHsts();
            }
            //app.UseHttpsRedirection();

            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.ToString();
                if (context.WebSockets.IsWebSocketRequest && webSocketHandlers.Keys.Contains(path))
                    await webSocketHandlers[path].Invoke(context, next);
                else
                    await next();
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action=Index}/{id?}");
            });
        }
    }

    #endregion
}
