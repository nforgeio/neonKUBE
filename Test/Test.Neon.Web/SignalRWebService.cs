//-----------------------------------------------------------------------------
// FILE:	    WebService.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

#if !NETCOREAPP3_1

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Service;
using Neon.Web.SignalR;
using Neon.Xunit;

using NATS.Client;

using Xunit;

namespace Test.Neon.SignalR
{
    public class Startup
    {
        private WebService service;
        private IConfiguration configuration;

        public Startup(IConfiguration configuration, WebService service)
        {
            this.configuration = configuration;
            this.service = service;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var natsServerUri = service.GetEnvironmentVariable("NATS_URI", string.Empty);

            var connectionFactory = new ConnectionFactory();
            var options = ConnectionFactory.GetDefaultOptions();
            
            options.Servers = new string[] { natsServerUri };

            var connection = connectionFactory.CreateConnection(options);

            var logger = service.LogManager.CreateLogger("neon-signalr");

            services
                .AddSingleton<IUserIdProvider, UserNameIdProvider>()
                .AddSingleton(logger)
                .AddSignalR()
                .AddNeonNats(connection);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<EchoHub>("/echo");
            });
        }

        private class UserNameIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection)
            {
                // This is an AWFUL way to authenticate users! We're just using it for test purposes.
                var userNameHeader = connection.GetHttpContext().Request.Headers["UserName"];
                if (!StringValues.IsNullOrEmpty(userNameHeader))
                {
                    return userNameHeader;
                }

                return null;
            }
        }
    }

    public class WebService : NeonService
    {
        private IWebHost webHost;
        public string NatsServerUri;

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
            // Load the configuration environment variables, exiting with a
            // non-zero exit code if they don't exist.

            NatsServerUri = Environment.Get("NATS_URI", string.Empty);

            if (string.IsNullOrEmpty(NatsServerUri))
            {
                Log.LogCritical("Invalid configuration: [NATS_URI] environment variable is missing or invalid.");
                Exit(1);
            }

            // Start the HTTP service.

            var endpoint = Description.Endpoints.Default;

            webHost = new WebHostBuilder()
                .UseStartup<Startup>()
                .UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, endpoint.Port);
                    })
                .ConfigureServices(services => services.AddSingleton(typeof(WebService), this))
                .Build();

            webHost.Start();

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}

#endif
