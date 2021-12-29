//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

using StackExchange.Redis;

using Prometheus;

using Yarp;

namespace NeonSsoSessionProxy
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public NeonSsoSessionProxyService NeonSsoSessionProxyService;

        public Startup(IConfiguration configuration, NeonSsoSessionProxyService service)
        {
            Configuration = configuration;
            this.NeonSsoSessionProxyService = service;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            if (NeonSsoSessionProxyService.InDevelopment)
            {
                services.AddDistributedMemoryCache();
            }
            else
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = "neon-redis.neon-system";
                    options.InstanceName = "neon-redis";
                    options.ConfigurationOptions = new ConfigurationOptions()
                    {
                        AllowAdmin = true,
                        ServiceName = "master"
                    };
                    options.ConfigurationOptions.EndPoints.Add("neon-redis.neon-system:26379");
                });
            }

            services.AddHealthChecks();
            services.AddHttpForwarder();
            services.AddHttpClient();

            // Dex config
            var configFile = NeonSsoSessionProxyService.GetConfigFilePath("/etc/neonkube/neon-sso-session-proxy/config.yaml");
            var config = NeonHelper.YamlDeserialize<dynamic>(File.ReadAllText(configFile));
            var dexClient = new DexClient(NeonSsoSessionProxyService.ServiceMap[KubeService.Dex].Endpoints.Default.Uri);
            
            // Load in each of the clients from the Dex config into the client.
            foreach (var client in config["staticClients"])
            {
                dexClient.AuthHeaders.Add(client["id"], new BasicAuthenticationHeaderValue(client["id"], client["secret"]));
            }

            services.AddSingleton(dexClient);

            // Http client for Yarp.
            var httpMessageInvoker = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            });
            services.AddSingleton(httpMessageInvoker);

            // Cookie encryption cipher.
            var aesCipher = new AesCipher(NeonSsoSessionProxyService.GetEnvironmentVariable("COOKIE_CIPHER", AesCipher.GenerateKey()));
            services.AddSingleton(aesCipher);

            services.AddControllers()
                .AddNeon();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (NeonSsoSessionProxyService.InDevelopment 
                || !string.IsNullOrEmpty(NeonSsoSessionProxyService.GetEnvironmentVariable("DEBUG")))
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseRouting();
            app.UseSsoSessionMiddleware();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/healthz");
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
            app.UseHttpMetrics();
        }
    }
}
