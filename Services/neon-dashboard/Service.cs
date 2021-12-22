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

namespace NeonDashboard
{
    /// <summary>
    /// Implements the Neon Dashboard service.
    /// </summary>
    public class NeonDashboardService : NeonService
    {
        /// <summary>
        /// Port to listen on.
        /// </summary>
        private static int webPort = 5000;

        // class fields
        private IWebHost webHost;

        /// <summary>
        /// Session cookie name.
        /// </summary>
        public const string sessionCookieName = ".NeonKUBE.Dashboard.Session.Cookie";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public NeonDashboardService(ServiceMap serviceMap, string name)
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
            // Start the web service.

            var endpoint = Description.Endpoints.Default;

            webHost = new WebHostBuilder()
                .UseStartup<Startup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, webPort))
                .ConfigureServices(services => services.AddSingleton(typeof(NeonDashboardService), this))
                .UseStaticWebAssets()
                .Build();

            webHost.Run();

            Log.LogInfo($"Listening on {IPAddress.Any}:{webPort}");

            // Indicate that the service is ready for business.

            await SetRunningAsync();
            Log.LogInfo("Service running");

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();

            // Return the exit code specified by the configuration.

            return await Task.FromResult(0);
        }
    }
}