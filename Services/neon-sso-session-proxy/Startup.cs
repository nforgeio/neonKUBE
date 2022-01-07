//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Distributed;

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
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class Startup
    {
        public IConfiguration               Configuration { get; }
        public NeonSsoSessionProxyService   NeonSsoSessionProxyService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public Startup(IConfiguration configuration, NeonSsoSessionProxyService service)
        {
            Configuration                   = configuration;
            this.NeonSsoSessionProxyService = service;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
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
                    options.Configuration        = "neon-redis.neon-system";
                    options.InstanceName         = "neon-redis";
                    options.ConfigurationOptions = new ConfigurationOptions()
                    {
                        AllowAdmin  = true,
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
            var config     = NeonHelper.YamlDeserialize<dynamic>(File.ReadAllText(configFile));
            var dexClient  = new DexClient(new Uri($"http://{KubeService.Dex}:5556"));
            
            // Load in each of the clients from the Dex config into the client.

            foreach (var client in config["staticClients"])
            {
                dexClient.AuthHeaders.Add(client["id"], new BasicAuthenticationHeaderValue(client["id"], client["secret"]));
            }

            services.AddSingleton(dexClient);

            // Http client for Yarp.

            var httpMessageInvoker = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy               = false,
                AllowAutoRedirect      = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies             = false
            });
            services.AddSingleton(httpMessageInvoker);

            // Cookie encryption cipher.

            var aesCipher = new AesCipher(NeonSsoSessionProxyService.GetEnvironmentVariable("COOKIE_CIPHER", AesCipher.GenerateKey()));

            services.AddSingleton(aesCipher);

            var cacheOptions = new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            };
            services.AddSingleton(cacheOptions);

            services.AddSingleton<SessionTransformer>(
                serviceProvider =>
                {
                    return new SessionTransformer(serviceProvider.GetService<IDistributedCache>(), aesCipher, dexClient, NeonSsoSessionProxyService.Log, cacheOptions);
                });
            
            services.AddControllers()
                .AddNeon();
        }

        /// <summary>
        /// Configures the operator web service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        /// <param name="env">Specifies the web hosting environment.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (NeonSsoSessionProxyService.InDevelopment || !string.IsNullOrEmpty(NeonSsoSessionProxyService.GetEnvironmentVariable("DEBUG")))
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseRouting();
            app.UseHttpMetrics(options =>
            {
                // This identifies the page when using Razor Pages.
                options.AddRouteParameter("page");
            });
            app.UseSsoSessionMiddleware();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/healthz");
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
        }
    }
}
