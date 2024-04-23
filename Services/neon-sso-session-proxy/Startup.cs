//-----------------------------------------------------------------------------
// FILE:        Startup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

using StackExchange.Redis;

using Prometheus;

using Yarp;
using Yarp.ReverseProxy.Forwarder;

namespace NeonSsoSessionProxy
{
    /// <summary>
    /// Configures the operator's service controllers.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Returns the configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Returns the SSO proxy service.
        /// </summary>
        public Service Service { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Specifies the service configuration.</param>
        /// <param name="service">Specifies the service.</param>
        public Startup(IConfiguration configuration, Service service)
        {
            this.Configuration = configuration;
            this.Service       = service;
        }

        /// <summary>
        /// Configures depdendency injection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            if (Service.InDevelopment)
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
            services.AddSingleton<ILogger>(Service.Logger);
            services.AddHealthChecks();
            services.AddHttpForwarder();
            services.AddHttpClient();
            services.AddSingleton(Service.DexClient);
            services.AddSingleton<ForwarderRequestConfig>();

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

            var aesCipher = new AesCipher(Service.GetEnvironmentVariable("COOKIE_CIPHER", AesCipher.GenerateKey(), redact: true));

            services.AddSingleton(aesCipher);

            var cacheOptions = new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            };
            services.AddSingleton(cacheOptions);

            services.AddSingleton<SessionTransformer>(
                serviceProvider =>
                {
                    return new SessionTransformer(serviceProvider.GetService<IDistributedCache>(), aesCipher, Program.Service.DexClient, Service.Logger, cacheOptions);
                });
            
            services.AddControllers()
                .AddNeon();

            Service.Logger.LogDebugEx("Services configured.");
        }

        /// <summary>
        /// Configures the service controllers.
        /// </summary>
        /// <param name="app">Specifies the application builder.</param>
        /// <param name="env">Specifies the web hosting environment.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (Service.DebugMode)
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseHttpMetrics();
            app.UseSsoSessionMiddleware();
            app.UseEndpoints(
                endpoints =>
                {
                    endpoints.MapHealthChecks("/healthz");
                    endpoints.MapControllers();
                });

            // Indicate that the service is ready for business.

            Service.Started();
        }
    }
}
