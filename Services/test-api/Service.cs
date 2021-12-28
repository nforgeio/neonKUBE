//-----------------------------------------------------------------------------
// FILE:	    Service.cs
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
using Neon.Net;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestApiService
{
    /// <summary>
    /// Implements a simple web service used for testing purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service simply listens on the port specified by the <b>PORT</b> environment variable
    /// if present or <b>port 80</b> by default and handles the following requests:
    /// </para>
    /// <list type="table"
    /// <item>
    ///     <term>/echo</term>
    ///     <description>
    ///     <para>
    ///     Echos text back to the caller:
    ///     </para>
    ///     <list type="number">
    ///         <item>the query string</item>
    ///         <item>the content included with the request</item>
    ///         <item>the constant string: <b>"HELLO WORLD!"</b> if neither of the above are present in the request</item>
    ///     </list>
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public class Service : NeonService
    {
        private IWebHost webHost;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="version">Specifies the service version.</param>
        public Service(string name, string version)
            : base(name, version: version)
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
            // Parse the environment variables.

            var portVariable = GetEnvironmentVariable("PORT", "80");

            if (!int.TryParse(portVariable, out var port) || !NetHelper.IsValidPort(port))
            {
                Log.LogCritical($"[PORT={port}] environment variable is not valid.");
                return 1;
            }

            // Start the HTTP service.

            webHost = new WebHostBuilder()
                .UseStartup<Startup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, port))
                .ConfigureServices(services => services.AddSingleton(typeof(Service), this))
                .Build();

            webHost.Start();

            // Start a do-nothing thread that we can use to set breakpoints
            // to verify that Bridge to Kubernetes works.

            var nothingThread = NeonHelper.StartThread(NothingThread);

            // Indicate that the service is ready for business.

            await StartedAsync();

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();
            nothingThread.Join();
            Terminator.ReadyToExit();

            // Return the exit code specified by the configuration.

            return await Task.FromResult(0);
        }

        /// <summary>
        /// Implements the do-nothing thread.
        /// </summary>
        private void NothingThread()
        {
            while (true)
            {
                if (Terminator.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}
