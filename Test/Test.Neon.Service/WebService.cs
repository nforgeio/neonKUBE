//-----------------------------------------------------------------------------
// FILE:	    WebService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.IO;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// Startup class for <see cref="WebService"/>.
    /// </summary>
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class WebServiceStartup
    {
        private WebService service;

        public WebServiceStartup(IConfiguration configuration, WebService service)
        {
            this.Configuration = configuration;
            this.service       = service;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Forward all requests to the parent service to have them
            // handled there.

            app.Run(async context => await service.OnWebRequest(context));
        }
    }

    /// <summary>
    /// Implements a simple web service with a single endpoint that returns a
    /// string specified by a configuration environment variable or file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service demonstrates how to deploy a service with an ASP.NET endpoint that
    /// uses environment variables or a configuration file to specify the string
    /// returned by the endpoint.
    /// </para>
    /// <para>
    /// The service looks for the <b>WEB_RESULT</b> environment variable and
    /// if present, will return the value as the endpoint response text.  Otherwise,
    /// the service will look for a configuration file at the logical path
    /// <b>/etc/web/response</b> and return its contents of present.  If neither
    /// the environment variable or file are present, the endpoint will return
    /// <b>UNCONFIGURED</b>.
    /// </para>
    /// <para>
    /// We'll use these settings to exercise the <see cref="NeonService"/> logical
    /// configuration capabilities.
    /// </para>
    /// </remarks>
    public class WebService : NeonService
    {
        private IWebHost    webHost;
        private string      responseText;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public WebService(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
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
            // Read the configuration environment variable or file to initialize
            // endpoint response text.

            responseText = "UNCONFIGURED";

            var resultVar = GetEnvironmentVariable("WEB_RESULT");

            if (resultVar != null)
            {
                responseText = resultVar;
            }
            else
            {
                var configPath = GetConfigFilePath("/etc/web/response");

                if (configPath != null && File.Exists(configPath))
                {
                    responseText = File.ReadAllText(configPath);
                }
            }

            // Start the HTTP service.

            var endpoint = Description.Endpoints.Default;

            webHost = new WebHostBuilder()
                .UseStartup<WebServiceStartup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, endpoint.Port))
                .ConfigureServices(services => services.AddSingleton(typeof(WebService), this))
                .Build();

            webHost.Start();

            // Indicate that the service is ready for business.

            await SetRunningAsync();

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();

            // Return the exit code specified by the configuration.

            return await Task.FromResult(0);
        }

        /// <summary>
        /// Handles web requests.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task OnWebRequest(HttpContext context)
        {
            await context.Response.WriteAsync(responseText);
        }
    }
}
