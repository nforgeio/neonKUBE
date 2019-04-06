//-----------------------------------------------------------------------------
// FILE:	    AspNetFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;

using Neon.Common;
using Neon.Data;
using Neon.Net;

namespace Neon.Xunit
{
    /// <summary>
    /// Fixture for testing ASP.NET Core based websites and services.
    /// </summary>
    public class AspNetFixture : TestFixture
    {
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public AspNetFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AspNetFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns a <see cref="JsonClient"/> suitable for querying the service.
        /// </summary>
        public JsonClient JsonClient { get; private set; }

        /// <summary>
        /// Returns an <see cref="HttpClient"/> suitable for querying the service.
        /// </summary>
        public HttpClient HttpClient => JsonClient.HttpClient;

        /// <summary>
        /// Returns the base URI for the running service.
        /// </summary>
        public Uri BaseAddress => JsonClient.BaseAddress;

        /// <summary>
        /// Returns the service's <see cref="IWebHost"/>.
        /// </summary>
        public IWebHost WebHost { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <typeparam name="TStartup">The startup class for the service.</typeparam>
        /// <param name="prestartAction">
        /// Optional action that will be called before the service is started to do things
        /// like initializing a <see cref="TestContext"/>.
        /// </param>
        public void Start<TStartup>(Action prestartAction = null)
            where TStartup : class
        {
            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            prestartAction?.Invoke();

            WebHost = new WebHostBuilder()
                .UseStartup<TStartup>()
                .UseKestrel(
                    options =>
                    {
                        // Pass [port=0] to have the OS to select a free port.

                        options.Listen(IPAddress.Loopback, 0);
                    })
                .Build();

            WebHost.Start();

            // Get the address where the server is listening and create the client.

            JsonClient = new JsonClient()
            {
                BaseAddress = new Uri(WebHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.OfType<string>().FirstOrDefault())
            };
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            if (!IsDisposed)
            {
                JsonClient.Dispose();
                WebHost.StopAsync().Wait();

                JsonClient = null;
                WebHost    = null;
            }
        }
    }
}
