//-----------------------------------------------------------------------------
// FILE:	    CadenceConnection.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a client to manage an Uber Cadence cluster.
    /// </summary>
    public class CadenceConnection : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        private class Startup
        {
            private CadenceConnection client;

            public void Configure(IApplicationBuilder app, CadenceConnection client)
            {
                this.client = client;

                app.Run(async context =>
                {
                    await client.OnHttpRequestAsync(context);
                });
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object       staticSyncLock = new object();
        private static Assembly     thisAssembly   = Assembly.GetExecutingAssembly();
        private static INeonLogger  log            = LogManager.Default.GetLogger<CadenceConnection>();
        private static string       proxyPath;

        /// <summary>
        /// Writes the correct <b>cadence-proxy</b> binary for the current environment
        /// to the file system (if that hasn't been done already) and then launches 
        /// a proxy instance configured to listen at the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The network endpoint where the proxy will listen.</param>
        /// <param name="settings">The cadence connection settings.</param>
        /// <returns>The proxy <see cref="Process"/>.</returns>
        /// <remarks>
        /// By default, this class will write the binary to the same directory where
        /// this assembly resides.  This should work for most circumstances.  On the
        /// odd change that the current application doesn't have write access to this
        /// directory, you may specify an alternative via <paramref name="settings"/>.
        /// </remarks>
        private static Process StartProxy(IPEndPoint endpoint, CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(endpoint != null);
            Covenant.Requires<ArgumentNullException>(settings != null);

            var binaryFolder = settings.BinaryFolder;

            if (binaryFolder == null)
            {
                binaryFolder = NeonHelper.GetAssemblyFolder(thisAssembly);
            }

            string resourcePath;
            string binaryPath;

            if (NeonHelper.IsWindows)
            {
                resourcePath = "Neon.Cadence.Resources.cadence-proxy.win.exe.gz";
                binaryPath   = Path.Combine(binaryFolder, "cadence-proxy.exe");
            }
            else if (NeonHelper.IsOSX)
            {
                resourcePath = "Neon.Cadence.Resources.cadence-proxy.osx.gz";
                binaryPath   = Path.Combine(binaryFolder, "cadence-proxy");
            }
            else if (NeonHelper.IsLinux)
            {
                resourcePath = "Neon.Cadence.Resources.cadence-proxy.linux.gz";
                binaryPath   = Path.Combine(binaryFolder, "cadence-proxy");
            }
            else
            {
                throw new NotImplementedException();
            }

            lock (staticSyncLock)
            {
                if (!File.Exists(binaryPath))
                {
                    // Extract and decompress the correct [cadence-proxy].

                    using (var resourceStream = thisAssembly.GetManifestResourceStream(resourcePath))
                    {
                        using (var binaryStream = new FileStream(binaryPath, FileMode.Create, FileAccess.ReadWrite))
                        {
                            NeonHelper.GzipTo(resourceStream, binaryStream);
                        }
                    }

                    if (NeonHelper.IsLinux || NeonHelper.IsOSX)
                    {
                        // We need to set the execute permissions on this file.  We're
                        // going to assume that only the root and current user will
                        // need to execute this.

                        var result = NeonHelper.ExecuteCapture("chmod", new object[] { "774", binaryPath });

                        if (result.ExitCode != 0)
                        {
                            throw new IOException($"Cannot set execute permissions for [{binaryPath}]:\r\n{result.ErrorText}");
                        }
                    }
                }
            }

            // Launch the proxy with a console window when we're running in DEBUG
            // mode on Windows.  We'll ignore this for the other platforms.

            if (NeonHelper.IsWindows)
            {
                var startInfo = new ProcessStartInfo(binaryPath, $"{endpoint.Address}:{endpoint.Port}")
                {
                    CreateNoWindow = !settings.Debug,
                };

                return Process.Start(startInfo);
            }
            else
            {
                return Process.Start(binaryPath, $"{endpoint.Address}:{endpoint.Port}");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private IWebHost    webHost;
        private IPAddress   proxyAddress = IPAddress.Parse("127.0.0.2");    // Using a non-standard loopback to avoid port conflicts
        private int         proxyPort;
        private Process     proxyProcess;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="CadenceSettings"/>.</param>
        public CadenceConnection(CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            this.Settings = settings;

            // Start the web server that will listen for requests from the associated 
            // Cadence Proxy process.

            webHost = new WebHostBuilder()
                .UseKestrel(
                    options =>
                    {
                        options.Listen(proxyAddress, settings.ListenPort);
                    })
                .ConfigureServices(
                    services =>
                    {
                        services.AddSingleton(typeof(CadenceConnection), this);
                        services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);
                    })
                .UseStartup<Startup>()
                .Build();

            webHost.Start();

            ListenUri = new Uri(webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.OfType<string>().FirstOrDefault());

            // Determine the port we'll have [cadence-proxy] listen on and then
            // fire up the proxy process.

            proxyPort    = NetHelper.GetUnusedTcpPort(proxyAddress);
            proxyProcess = StartProxy(new IPEndPoint(proxyAddress, proxyPort), settings);
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~CadenceConnection()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (webHost != null)
            {
                webHost.Dispose();
                webHost = null;
            }

            if (proxyProcess != null)
            {
                proxyProcess.Kill();
                proxyProcess.WaitForExit();
                proxyProcess = null;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the settings used to create the client.
        /// </summary>
        public CadenceSettings Settings { get; private set; }

        /// <summary>
        /// Returns the URI the client is listening on for requests from the Cadence Proxy.
        /// </summary>
        public Uri ListenUri { get; private set; }

        /// <summary>
        /// Called when an HTTP request is received by the integrated web server 
        /// (presumably from the the associated Cadence Proxy process).
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnHttpRequestAsync(HttpContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            if (request.Method != "PUT")
            {
                response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await response.WriteAsync($"[{request.Method}] HTTP method is not supported.  All requests must be submitted with [PUT].");
                return;
            }

            if (request.ContentType != ProxyMessage.ContentType)
            {
                response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await response.WriteAsync($"[{request.ContentType}] Content-Type is not supported.  All requests must be submitted with [Content-Type={request.ContentType}].");
                return;
            }

            try
            {
                switch (request.Path)
                {
                    case "/":

                        await OnRootRequestAsync(context);
                        break;

                    case "/echo":

                        await OnEchoRequestAsync(context);
                        break;

                    default:

                        response.StatusCode = StatusCodes.Status404NotFound;
                        await response.WriteAsync($"[{request.Path}] HTTP PATH not supported.  Only [/] and [/echo] are allowed.");
                        return;
                }
            }
            catch (FormatException e)
            {
                log.LogError(e);
                response.StatusCode = StatusCodes.Status400BadRequest;
            }
            catch (Exception e)
            {
                log.LogError(e);
                response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }

        /// <summary>
        /// Handles requests to the root "<b>"/"</b> endpoint path.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnRootRequestAsync(HttpContext context)
        {
            var request        = context.Request;
            var response       = context.Response;
            var requestMessage = ProxyMessage.Deserialize<ProxyMessage>(request.Body);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles requests to the test "<b>"/echo"</b> endpoint path.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEchoRequestAsync(HttpContext context)
        {
            var request        = context.Request;
            var response       = context.Response;
            var requestMessage = ProxyMessage.Deserialize<ProxyMessage>(request.Body);
            var clonedMessage  = requestMessage.Clone();

            response.ContentType = ProxyMessage.ContentType;

            await response.Body.WriteAsync(clonedMessage.Serialize());
        }
    }
}
