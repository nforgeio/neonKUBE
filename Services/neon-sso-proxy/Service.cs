//-----------------------------------------------------------------------------
// FILE:	    Service.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System.Threading.Tasks;
using System.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Service;
using Neon.Common;

using Prometheus;
using Prometheus.DotNetRuntime;

namespace NeonSsoProxy
{
    /// <summary>
    /// Implements the Neon Dashboard service.
    /// </summary>
    public class NeonSsoProxyService : NeonService
    {
        // class fields
        private IWebHost webHost;

        /// <summary>
        /// Session cookie name.
        /// </summary>
        public const string SessionCookieName = ".NeonKUBE.SsoProxy.Session.Cookie";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public NeonSsoProxyService(ServiceMap serviceMap, string name)
             : base(name,
                  $@"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}{(ThisAssembly.Git.IsDirty ? "-dirty" : "")}",
                  serviceMap: serviceMap)
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
            
            var endpoint = Description.Endpoints.Default;

            if (!NeonHelper.IsDevWorkstation)
            {
                MetricsOptions.Mode = MetricsMode.Scrape;
                MetricsOptions.Path = "/metrics";
                MetricsOptions.Port = endpoint.Port + 1;

                MetricsOptions.GetCollector = () =>
                                DotNetRuntimeStatsBuilder
                                    .Default()
                                    .StartCollecting();
            }

            // Start the web service.

            webHost = new WebHostBuilder()
                .UseStartup<Startup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, endpoint.Port))
                .ConfigureServices(services => services.AddSingleton(typeof(NeonSsoProxyService), this))
                .UseStaticWebAssets()
                .Build();

            _ = webHost.RunAsync();

            Log.LogInfo($"Listening on {IPAddress.Any}:{endpoint.Port}");

            // Indicate that the service is ready for business.

            await SetStatusAsync(NeonServiceStatus.Running);
            Log.LogInfo("Service running");

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();

            // Return the exit code specified by the configuration.

            return await Task.FromResult(0);
        }
    }
}