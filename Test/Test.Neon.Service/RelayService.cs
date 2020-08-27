//-----------------------------------------------------------------------------
// FILE:	    RelayService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net.Http;
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
    /// Startup class for <see cref="RelayService"/>.
    /// </summary>
    public class RelayServiceStartup
    {
        private RelayService service;

        public RelayServiceStartup(IConfiguration configuration, RelayService service)
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
    /// Implements a simple web service that demonstrates how services can use 
    /// the <see cref="ServiceMap"/> to query another service.  This service
    /// simply relays the request it receives the the endpoint exposed by
    /// the service named <b>web-service</b> whose description will be in
    /// the <see cref="ServiceMap"/> passed to the service.
    /// </summary>
    public class RelayService : NeonService
    {
        private IWebHost        webHost;
        private HttpClient      httpClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        /// <param name="branch">Optionally specifies the build branch.</param>
        /// <param name="commit">Optionally specifies the branch commit.</param>
        /// <param name="isDirty">Optionally specifies whether there are uncommit changes to the branch.</param>
        public RelayService(ServiceMap serviceMap, string name, string branch = null, string commit = null, bool isDirty = false)
            : base(serviceMap, name, branch, commit, isDirty)
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

            // We'll also dispose the HTTP client.

            if (httpClient != null)
            {
                httpClient.Dispose();
                httpClient = null;
            }
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Query the service map for the [web-service] endpoint and setup
            // the HTTP client we'll use to communicate with that service.

            var webService = ServiceMap["web-service"];

            if (webService == null)
            {
                Log.LogError("Service description for [web-service] not found.");
                Exit(1);
            }

            httpClient = new HttpClient()
            {
                BaseAddress = webService.Endpoints.Default.Uri
            };

            // Start the HTTP service.

            var endpoint = Description.Endpoints.Default;

            webHost = new WebHostBuilder()
                .UseStartup<RelayServiceStartup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, endpoint.Port))
                .ConfigureServices(services => services.AddSingleton(typeof(RelayService), this))
                .Build();

            webHost.Start();

            // Indicate that the service is ready for business.

            await SetRunningAsync();

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();

            return 0;
        }

        /// <summary>
        /// Handles web requests.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task OnWebRequest(HttpContext context)
        {
            // Call the [web-service] and return what it returns back 
            // to our caller.

            var response = context.Response;

            try
            {
                var remoteResponse = await httpClient.GetStringAsync("/");

                await response.WriteAsync(remoteResponse);
            }
            catch (Exception e)
            {
                response.StatusCode = StatusCodes.Status500InternalServerError;

                await response.WriteAsync(NeonHelper.ExceptionError(e));
            }
        }
    }
}
