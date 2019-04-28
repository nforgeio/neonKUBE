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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
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
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a client to manage an Uber Cadence cluster.
    /// </summary>
    public partial class CadenceConnection : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Configures the <b>cadence-client</b> connection's web server used to 
        /// receive messages from the <b>cadence-proxy</b>.
        /// </summary>
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

        /// <summary>
        /// Configures a partially implemented emulation of a <b>cadence-proxy</b>
        /// for low-level testing.
        /// </summary>
        private class EmulatedStartup
        {
            private CadenceConnection client;

            public void Configure(IApplicationBuilder app, CadenceConnection client)
            {
                this.client = client;

                app.Run(async context =>
                {
                    await client.OnEmulatedHttpRequestAsync(context);
                });
            }
        }

        /// <summary>
        /// Used for tracking pending <b>cadence-proxy</b> operations.
        /// </summary>
        private class Operation
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="requestId">The unique request ID.</param>
            /// <param name="request">The request message.</param>
            /// <param name="timeout">
            /// Optionally specifies the timeout.  This defaults to the end of time.
            /// </param>
            public Operation(long requestId, ProxyRequest request, TimeSpan timeout = default)
            {
                Covenant.Requires<ArgumentNullException>(request != null);

                this.CompletionSource = new TaskCompletionSource<ProxyReply>();
                this.RequestId        = requestId;
                this.StartTimeUtc     = DateTime.UtcNow;
                this.Timeout          = timeout.AdjustToFitDateRange(StartTimeUtc);
            }

            /// <summary>
            /// The operation (aka the request) ID.
            /// </summary>
            public long RequestId { get; private set; }

            /// <summary>
            /// Returns the request message.
            /// </summary>
            public ProxyRequest Request { get; private set; }

            /// <summary>
            /// The time (UTC) the operation started.
            /// </summary>
            public DateTime StartTimeUtc { get; private set; }

            /// <summary>
            /// The operation timeout. 
            /// </summary>
            public TimeSpan Timeout { get; private set; }

            /// <summary>
            /// Returns the <see cref="TaskCompletionSource{ProxyReply}"/> that we'll use
            /// to signal completion when <see cref="SetReply(ProxyReply)"/> is called
            /// with the reply message for this operation, <see cref="SetCanceled"/> when
            /// the operation has been canceled, or <see cref="SetException(Exception)"/>
            /// is called signalling an error.
            /// </summary>
            public TaskCompletionSource<ProxyReply> CompletionSource { get; private set; }

            /// <summary>
            /// Signals the awaiting <see cref="Task"/> that a reply message 
            /// has been received.
            /// </summary>
            /// <param name="reply">The reply message.</param>
            /// <remarks>
            /// <note>
            /// Only the first call to <see cref="SetReply(ProxyReply)"/>
            /// <see cref="SetException(Exception)"/>, or <see cref="SetCanceled()"/>
            /// will actually wake the awaiting task.  Any subsequent calls will do nothing.
            /// </note>
            /// </remarks>
            public void SetReply(ProxyReply reply)
            {
                Covenant.Requires<ArgumentNullException>(reply != null);

                lock (this)
                {
                    if (CompletionSource == null)
                    {
                        return;
                    }

                    CompletionSource.TrySetResult(reply);
                    CompletionSource = null;
                }
            }

            /// <summary>
            /// Signals the awaiting <see cref="Task"/> that the operation has
            /// been canceled.
            /// </summary>
            /// <remarks>
            /// <note>
            /// Only the first call to <see cref="SetReply(ProxyReply)"/>
            /// <see cref="SetException(Exception)"/>, or <see cref="SetCanceled()"/>
            /// will actually wake the awaiting task.  Any subsequent calls will do nothing.
            /// </note>
            /// </remarks>
            public void SetCanceled()
            {
                lock (this)
                {
                    if (CompletionSource == null)
                    {
                        return;
                    }

                    CompletionSource.TrySetCanceled();
                    CompletionSource = null;
                }
            }

            /// <summary>
            /// Signals the awaiting <see cref="Task"/> that it should fail
            /// with an exception.
            /// </summary>
            /// <param name="e">The exception.</param>
            /// <remarks>
            /// <note>
            /// Only the first call to <see cref="SetReply(ProxyReply)"/>
            /// <see cref="SetException(Exception)"/>, or <see cref="SetCanceled()"/>
            /// will actually wake the awaiting task.  Any subsequent calls will do nothing.
            /// </note>
            /// </remarks>
            public void SetException(Exception e)
            {
                Covenant.Requires<ArgumentNullException>(e != null);

                lock (this)
                {
                    if (CompletionSource == null)
                    {
                        return;
                    }

                    CompletionSource.TrySetException(e);
                    CompletionSource = null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object       staticSyncLock = new object();
        private static Assembly     thisAssembly   = Assembly.GetExecutingAssembly();
        private static INeonLogger  log            = LogManager.Default.GetLogger<CadenceConnection>();

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

            if (!NeonHelper.Is64Bit)
            {
                throw new Exception("[Neon.Cadence] supports 64-bit applications only.");
            }

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
                            resourceStream.GunzipTo(binaryStream);
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

            var debugOption = settings.Debug ? " --debug" : string.Empty;
            var commandLine = $"--listen {endpoint.Address}:{endpoint.Port} --log-level {settings.LogLevel}{debugOption}";

            if (NeonHelper.IsWindows)
            {
                var startInfo = new ProcessStartInfo(binaryPath, commandLine)
                {
                    UseShellExecute = settings.Debug,
                };

                return Process.Start(startInfo);
            }
            else
            {
                return Process.Start(binaryPath, commandLine);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                      syncLock     = new object();
        private IPAddress                   proxyAddress = IPAddress.Parse("127.0.0.2");    // Using a non-default loopback to avoid port conflicts
        private int                         proxyPort;
        private Process                     proxyProcess;
        private HttpClient                  proxyClient;
        private IWebHost                    host;
        private IWebHost                    emulatedHost;
        private long                        nextRequestId;
        private Dictionary<long, Operation> operations;
        private Thread                      backgroundThread;
        private bool                        closingConnection;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="CadenceSettings"/>.</param>
        public CadenceConnection(CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            this.Settings = settings;

            // Start the web server that will listen for requests from the associated 
            // [cadence-proxy] process.

            host = new WebHostBuilder()
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

            host.Start();

            ListenUri = new Uri(host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.OfType<string>().FirstOrDefault());

            // Determine the port we'll have [cadence-proxy] listen on and then
            // fire up the cadence-proxy process or the stubbed host.

            proxyPort = NetHelper.GetUnusedTcpPort(proxyAddress);

            if (!settings.EmulateProxy)
            {
                proxyProcess = StartProxy(new IPEndPoint(proxyAddress, proxyPort), settings);
            }
            else
            {
                // Start up a partially implemented emulation of a cadence-proxy.

                emulatedHost = new WebHostBuilder()
                    .UseKestrel(
                        options =>
                        {
                            options.Listen(proxyAddress, proxyPort);
                        })
                    .ConfigureServices(
                        services =>
                        {
                            services.AddSingleton(typeof(CadenceConnection), this);
                            services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);
                        })
                    .UseStartup<EmulatedStartup>()
                    .Build();

                emulatedHost.Start();
            }

            // Create the HTTP client we'll use to communicate with the [cadence-proxy].

            var httpHandler = new HttpClientHandler()
            {
                // Disable compression because all communication is happening on
                // a loopback interface (essentially in-memory) so there's not
                // much point in taking the CPU hit to manage compression.

                AutomaticDecompression = DecompressionMethods.None
            };

            proxyClient = new HttpClient(httpHandler, disposeHandler: true);

            // Initialize the pending operations.

            nextRequestId     = 0;
            operations = new Dictionary<long, Operation>();

            // Crank up the background thread which will handle [cadence-proxy]
            // health heartbeats as well as request timeouts.

            backgroundThread = new Thread(new ThreadStart(BackgroundThread));
            backgroundThread.Start();
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
            closingConnection = true;

            if (backgroundThread != null)
            {
                backgroundThread.Join();
                backgroundThread = null;
            }

            if (host != null)
            {
                host.Dispose();
                host = null;
            }

            if (emulatedHost != null)
            {
                emulatedHost.Dispose();
                emulatedHost = null;
            }

            if (proxyProcess != null)
            {
                proxyProcess.Kill();
                proxyProcess.WaitForExit();
                proxyProcess = null;
            }

            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }

            if (EmulatedCadenceClient != null)
            {
                EmulatedCadenceClient.Dispose();
                EmulatedCadenceClient = null;
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
        /// Returns the URI the client is listening on for requests from the <b>cadence-proxy</b>.
        /// </summary>
        public Uri ListenUri { get; private set; }

        /// <summary>
        /// Returns the URI the associated <b>cadence-proxy</b> instance is listening on.
        /// </summary>
        public Uri ProxyUri => new Uri($"http://{proxyAddress}:{proxyPort}");

        /// <summary>
        /// Raised when the connection is closed.  You can determing whether the connection
        /// was closed normally or due to an error by examining the <see cref="CadenceConnectionClosedArgs"/>
        /// arguments passed to the handler.
        /// </summary>
        public event OnCadenceConnectionClosed ConnectionClosed;

        /// <summary>
        /// Called when an HTTP request is received by the integrated web server 
        /// (presumably from the the associated <b>cadence-proxy</b> process).
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
                        await response.WriteAsync($"[{request.Path}] HTTP PATH is not supported.  Only [/] and [/echo] are allowed.");
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
        /// Handles requests to the root <b>"/"</b> endpoint path.
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
        /// Handles requests to the test <b>"/echo"</b> endpoint path.
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

        /// <summary>
        /// Asynchronously calls the <b>cadence-proxy</b> by sending a request message
        /// and then waits for a reply.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">
        /// Optionally specifies the maximum time to wait for the operation to complete.
        /// This defaults to unlimited.
        /// </param>
        /// <returns>The reply message.</returns>
        private async Task<ProxyReply> CallProxyAsync(ProxyRequest request, TimeSpan timeout = default)
        {
            var requestId = Interlocked.Increment(ref this.nextRequestId);
            var operation = new Operation(requestId, request, timeout);

            lock (syncLock)
            {
                operations.Add(requestId, operation);
            }

            return await operation.CompletionSource.Task;
        }

        /// <summary>
        /// Implements the connection's background thread which is responsible
        /// for checking [cadence-proxy] health via heartbeat requests and 
        /// also for implelementing request timeouts.
        /// </summary>
        private void BackgroundThread()
        {
            Task.Run(
                async () =>
                {
                    var sleepTime = TimeSpan.FromSeconds(1);
                    var exception = (Exception)null;

                    try
                    {
                        while (!closingConnection)
                        {
                            Thread.Sleep(sleepTime);

                            // Verify the [cadence-proxy] health via be sending a heartbeat
                            // and waiting a bit for a reply.

                            try
                            {
                                var heartbeatReply = await CallProxyAsync(new HeartbeatRequest(), timeout: TimeSpan.FromSeconds(5));

                                if (heartbeatReply.ErrorType != CadenceErrorTypes.None)
                                {
                                    throw new Exception($"[cadence-proxy]: Heartbeat returns [{heartbeatReply.ErrorType}].");
                                }
                            }
                            catch (Exception e)
                            {
                                log.LogError("Heartbeat check failed.  Closing cadence connection.", e);
                                exception = e;

                                // Break out of the while loop so we'll signal the application that
                                // the connection has closed and then exit the thread below.

                                break;
                            }

                            // Look for any operations that have been running longer than
                            // the specified timeout and then individually cancel and
                            // remove them, and then notify the application that they were
                            // cancelled.

                            var timedOutOperations = new List<Operation>();
                            var utcNow             = DateTime.UtcNow;

                            lock (syncLock)
                            {
                                foreach (var operation in operations.Values)
                                {
                                    if (operation.Timeout <= TimeSpan.Zero)
                                    {
                                        // These operations can run indefinitely.

                                        continue;
                                    }

                                    if (operation.StartTimeUtc + operation.Timeout <= utcNow)
                                    {
                                        timedOutOperations.Add(operation);
                                    }
                                }

                                foreach (var operation in timedOutOperations)
                                {
                                    operations.Remove(operation.RequestId);
                                }
                            }

                            foreach (var operation in timedOutOperations)
                            {
                                // Send a cancel to the [cadence-proxy] for each timed-out
                                // operation, wait for the reply and then signal the client
                                // application that the operation was cancelled.
                                //
                                // Note that we're not sending a new CancelRequest for another
                                // CancelRequest that timed out to the potential of a blizzard
                                // of CancelRequests.
                                //
                                // Note that we're going to have all of these cancellations
                                // run in parallel rather than waiting for them to complete
                                // one-by-one.

                                log.LogWarn(() => $" Request Timeout: [request={operation.Request.GetType().Name}, started={operation.StartTimeUtc.ToString(NeonHelper.DateFormatTZ)}, timeout={operation.Timeout}].");

                                var notAwaitingThis = Task.Run(
                                    async () =>
                                    {
                                        if (operation.Request.Type != MessageTypes.CancelRequest)
                                        {
                                            await CallProxyAsync(new CancelRequest() { TargetRequestId = operation.RequestId }, timeout: TimeSpan.FromSeconds(1));
                                        }

                                        operation.SetCanceled();
                                    });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // We shouldn't see any exceptions here except perhaps
                        // [TaskCanceledException] when the connection is in
                        // the process of being closed.

                        if (!closingConnection || !(e is TaskCanceledException))
                        {
                            exception = e;
                            log.LogError(e);
                        }
                    }

                    // This is a good place to signal the client application that the
                    // connection has been closed.

                    ConnectionClosed?.Invoke(this, new CadenceConnectionClosedArgs() { Exception = exception });
                });
        }
    }
}
