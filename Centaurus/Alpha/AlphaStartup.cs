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

namespace Centaurus
{
    public class AlphaStartup : IStartup<AlphaSettings>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private IHost host;
        private ManualResetEvent resetEvent;

        public void Run(AlphaSettings settings, ManualResetEvent resetEvent)
        {
            this.resetEvent = resetEvent;
            ConfigureConstellation(settings);

            host = CreateHostBuilder(settings).Build();
            _ = host.RunAsync();
        }

        public void Shutdown()
        {
            if (host != null)
            {
                ConnectionManager.CloseAllConnections();
                InfoConnectionManager.CloseAllConnections();
                host.StopAsync().Wait();
                host = null;
            }
        }

        #region Private Members

        private void ConfigureConstellation(AlphaSettings settings)
        {
            Global.Init(settings, new MongoStorage());

            Global.AppState.StateChanged += Current_StateChanged;

            MessageHandlers<AlphaWebSocketConnection>.Init();
        }

        private void Current_StateChanged(object sender, ApplicationState e)
        {
            if (e == ApplicationState.Failed)
            {
                logger.Error("Application failed");
                resetEvent.Set();
            }
        }

        private IHostBuilder CreateHostBuilder(AlphaSettings settings)
        {
            return Host.CreateDefaultBuilder()
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
                    webBuilder.UseStartup<Startup>()
                        .UseUrls(settings.AlphaUrl);
                });
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
                if (Global.AppState == null || ValidApplicationStates.Contains(Global.AppState.State))
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await ConnectionManager.OnNewConnection(webSocket, context.Connection.RemoteIpAddress.ToString());
                }
                else
                {
                    context.Abort();
                }
            }

            static async Task InfoWebSocketHandler(HttpContext context, Func<Task> next)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await InfoConnectionManager.OnNewConnection(webSocket, context.Connection.Id, context.Connection.RemoteIpAddress.ToString());
            }

            // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }
                else
                {
                    app.UseExceptionHandler("/Error");
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

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
}
