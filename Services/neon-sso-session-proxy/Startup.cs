//-----------------------------------------------------------------------------
// FILE:	    Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
        public Service   NeonSsoSessionProxyService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public Startup(IConfiguration configuration, Service service)
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
            services.AddSingleton<ILogger>(Program.Service.Logger);
            services.AddHealthChecks();
            services.AddHttpForwarder();
            services.AddHttpClient();

            // Dex config

            var dexClient  = new DexClient(new Uri($"http://{KubeService.Dex}:5556"), NeonSsoSessionProxyService.Logger);
            
            // Load in each of the clients from the Dex config into the client.

            foreach (var client in NeonSsoSessionProxyService.Clients)
            {
                dexClient.AuthHeaders.Add(client.Spec.Id, new BasicAuthenticationHeaderValue(client.Spec.Id, client.Spec.Secret));
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

            var aesCipher = new AesCipher(NeonSsoSessionProxyService.GetEnvironmentVariable("COOKIE_CIPHER", AesCipher.GenerateKey(), redact: true));

            services.AddSingleton(aesCipher);

            var cacheOptions = new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            };
            services.AddSingleton(cacheOptions);

            services.AddSingleton<SessionTransformer>(
                serviceProvider =>
                {
                    return new SessionTransformer(serviceProvider.GetService<IDistributedCache>(), aesCipher, dexClient, NeonSsoSessionProxyService.Logger, cacheOptions);
                });
            
            services.AddControllers()
                .AddNeon();
        }

        /// <summary>
        /// Configures the service controllers.
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
                endpoints.MapControllers();
            });
        }
    }
}
