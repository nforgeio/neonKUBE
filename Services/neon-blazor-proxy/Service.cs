//-----------------------------------------------------------------------------
// FILE:	    Service.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Service;

using DnsClient;

using Prometheus;
using Prometheus.DotNetRuntime;

namespace NeonBlazorProxy
{
    /// <summary>
    /// Implements the <b>neon-blazor-proxy</b> service.
    /// </summary>
    public class Service : NeonService
    {
        /// <summary>
        /// The Default <see cref="NeonService"/> name.
        /// </summary>
        public const string ServiceName = "neon-blazor-proxy";

        /// <summary>
        /// Config file location.
        /// </summary>
        public const string ConfigFile = "/etc/neonkube/neon-blazor-proxy/config.yaml";

        /// <summary>
        /// Session cookie name.
        /// </summary>
        public const string SessionCookieName = ".Neon.Blazor.Proxy.Cookie";

        /// <summary>
        /// Proxy Configuration.
        /// </summary>
        public ProxyConfig Config;

        /// <summary>
        /// Dns Client.
        /// </summary>
        public LookupClient DnsClient;

        /// <summary>
        /// AES Cipher.
        /// </summary>
        public AesCipher AesCipher;

        /// <summary>
        /// HashSet containing current open websocket connection IDs. 
        /// </summary>
        public HashSet<string> CurrentConnections;

        /// <summary>
        /// Lock used for updating the load balancer status.
        /// </summary>
        public static readonly object ServerLock = new object();

        /// <summary>
        /// The host name of the last server to be sent a request.
        /// </summary>
        public static string LastServer { get; set; }

        // private fields
        private IWebHost webHost;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
             : base(name, version: "0.1", metricsPrefix: "neonblazorproxy")
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Dispose web host if it's still running.

            if (webHost != null)
            {
                webHost.Dispose();
                webHost = null;
            }
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            await SetStatusAsync(NeonServiceStatus.Starting);

            Config = await ProxyConfig.FromFileAsync(GetConfigFilePath(ConfigFile));

            DnsClient = new LookupClient(new LookupClientOptions()
            {
                UseCache            = Config.Dns.UseCache,
                MaximumCacheTimeout = TimeSpan.FromSeconds(Config.Dns.MaximumCacheTimeoutSeconds),
                MinimumCacheTimeout = TimeSpan.FromSeconds(Config.Dns.MinimumCacheTimeoutSeconds),
                CacheFailedResults  = Config.Dns.CacheFailedResults
            });

            AesCipher = new AesCipher(GetEnvironmentVariable("COOKIE_CIPHER", AesCipher.GenerateKey(), redacted: !Log.IsLogDebugEnabled));

            CurrentConnections = new HashSet<string>();

            // Start the web service.

            webHost = new WebHostBuilder()
                .ConfigureAppConfiguration(
                    (hostingcontext, config) =>
                    {
                        config.Sources.Clear();
                    })
                .UseStartup<Startup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, Config.Port))
                .ConfigureServices(services => services.AddSingleton(typeof(Service), this))
                .UseStaticWebAssets()
                .Build();

            _ = webHost.RunAsync();

            Log.LogInfo($"Listening on {IPAddress.Any}:{Config.Port}");

            // Indicate that the service is ready for business.

            await SetStatusAsync(NeonServiceStatus.Running);

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}