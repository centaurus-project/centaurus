using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog.Web;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Centaurus.Alpha
{
    public class AlphaHostBuilder : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaHostBuilder(ExecutionContext context)
            : base(context)
        {

        }

        public IHost CreateHost()
        {
            SetupCertificate(Context.Settings);
            return Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {

                    logging.ClearProviders();
                    logging.AddConsole();

                    //if (Context.Settings.Verbose)
                    //    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    //else if (Context.Settings.Silent)
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Error);
                    //else
                    //    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);

                })
                .UseNLog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureServices(services => services.AddSingleton(Context))
                        .UseStartup<HostStartup>()
                        .UseKestrel(options =>
                        {
                            if (Certificate != null)
                            {
                                options.ListenAnyIP(Context.Settings.ListeningPort,
                                listenOptions =>
                                {
                                    var httpsOptions = new HttpsConnectionAdapterOptions();
                                    httpsOptions.ServerCertificateSelector = (context, path) => Certificate;
                                    listenOptions.UseHttps(httpsOptions);
                                });
                            }
                            else
                                options.ListenAnyIP(Context.Settings.ListeningPort);
                        });
                }).Build();
        }

        public static State[] ValidStates = new State[] { State.Rising, State.Running, State.Ready };

        private void SetupCertificate(Settings alphaSettings)
        {
            if (!alphaSettings.UseSecureConnection)
                return;

            if (string.IsNullOrEmpty(alphaSettings.TlsCertificatePath))
                throw new Exception("Certificate path is not specified.");

            if (!File.Exists(alphaSettings.TlsCertificatePath))
                throw new FileNotFoundException($"Failed to find a certificate \"{alphaSettings.TlsCertificatePath}\"");

            UpdateCertificate(alphaSettings.TlsCertificatePath, alphaSettings.TlsCertificatePrivateKeyPath);

            ObserveCertificateChange(alphaSettings.TlsCertificatePath, alphaSettings.TlsCertificatePrivateKeyPath);
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

        class HostStartup
        {
            public HostStartup(IConfiguration configuration)
            {
                Configuration = configuration;
            }

            public IConfiguration Configuration { get; }

            // This method gets called by the runtime. Use this method to add services to the container.
            // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
            public void ConfigureServices(IServiceCollection services)
            {
                services
                    .AddMvc(options => options.EnableEndpointRouting = false)
                    .AddControllersAsServices()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

                services.Add(
                    new ServiceDescriptor(
                        typeof(IActionResultExecutor<JsonResult>),
                        Type.GetType("Microsoft.AspNetCore.Mvc.Infrastructure.SystemTextJsonResultExecutor, Microsoft.AspNetCore.Mvc.Core"),
                        ServiceLifetime.Singleton)
                );
                services.AddOptions<HostOptions>().Configure(opts => opts.ShutdownTimeout = TimeSpan.FromDays(365));

                services.AddControllers(opts =>
                {
                    opts.ModelBinderProviders.Insert(0, new XdrModelBinderProvider());
                });
            }

            static async Task CentaurusWebSocketHandler(HttpContext context, Func<Task> next)
            {
                var centaurusContext = context.RequestServices.GetService<ExecutionContext>();
                if (centaurusContext.StateManager != null 
                    && ValidStates.Contains(centaurusContext.StateManager.State) 
                    && TryGetPubKey(context, out var pubKey))
                {
                    using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
                        await centaurusContext.IncomingConnectionManager.OnNewConnection(webSocket,
                            pubKey, 
                            context.Connection.RemoteIpAddress.ToString());
                }
                else
                {
                    context.Abort();
                }
            }

            static bool TryGetPubKey(HttpContext context, out RawPubKey pubKey)
            {
                pubKey = null;
                if (context == null)
                    throw new ArgumentNullException(nameof(context));
                if (!(context.Request.Query.TryGetValue(WebSocketConstants.PubkeyParamName, out var address)
                    && KeyPair.TryGetFromAccountId(address, out var keyPair)))
                {
                    logger.Trace($"Unable to get pubkey from request {context.Request.Path}.");
                    return false;
                }
                pubKey = keyPair;
                return true;
            }

            static async Task InfoWebSocketHandler(HttpContext context, Func<Task> next)
            {
                var centaurusContext = context.RequestServices.GetService<ExecutionContext>();
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await centaurusContext.InfoConnectionManager.OnNewConnection(webSocket, context.Connection.Id, context.Connection.RemoteIpAddress.ToString());
            }

            static Dictionary<string, Func<HttpContext, Func<Task>, Task>> webSocketHandlers = new Dictionary<string, Func<HttpContext, Func<Task>, Task>>
            {
                { WebSocketConstants.CentaurusWebSocketEndPoint, CentaurusWebSocketHandler },
                { WebSocketConstants.InfoWebSocketEndPoint, InfoWebSocketHandler }
            };

            // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                app.UseHostFiltering();

                if (env.IsDevelopment())
                    app.UseDeveloperExceptionPage();
                else
                    app.UseExceptionHandler("/Error");

                app.UseWebSockets();

                app.Use(async (context, next) =>
                {
                    logger.Trace($"New connection received. Path: {context.Request.Path}");
                    var path = context.Request.Path.ToString();
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        if (webSocketHandlers.Keys.Contains(path))
                        {
                            logger.Trace($"Handler for path `{context.Request.Path}` found.");
                            await webSocketHandlers[path].Invoke(context, next);
                        }
                        else
                        {
                            logger.Trace($"Handler for path `{context.Request.Path}` not found.");
                        }
                    }
                    else
                    {
                        await next();
                    }
                });

                app.UseMvc(routes =>
                {
                    routes.MapRoute(
                        name: "default",
                        template: "{controller}/{action=Index}/{id?}");
                });
            }
        }
    }
}
