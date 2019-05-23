//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.cs
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

using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a client to manage an Uber Cadence cluster.
    /// </summary>
    public partial class CadenceClient : IDisposable
    {
        /// <summary>
        /// The <b>cadence-proxy</b> listening port to use when <see cref="CadenceSettings.DebugPrelaunched"/>
        /// mode is enabled.
        /// </summary>
        private const int debugProxyPort = 5000;

        /// <summary>
        /// The <b>cadence-client</b> listening port to use when <see cref="CadenceSettings.DebugPrelaunched"/>
        /// mode is enabled.
        /// </summary>
        private const int debugClientPort = 5001;

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Configures the <b>cadence-client</b> connection's web server used to 
        /// receive messages from the <b>cadence-proxy</b>.
        /// </summary>
        private class Startup
        {
            private CadenceClient client;

            public void Configure(IApplicationBuilder app, CadenceClient client)
            {
                this.client = client;

                app.Run(async context =>
                {
                    await client.OnHttpRequestAsync(context);
                });
            }
        }

#if DEBUG
        /// <summary>
        /// Configures a partially implemented emulation of a <b>cadence-proxy</b>
        /// for low-level testing.
        /// </summary>
        private class EmulatedStartup
        {
            private CadenceClient client;

            public void Configure(IApplicationBuilder app, CadenceClient client)
            {
                this.client = client;

                app.Run(async context =>
                {
                    await client.OnEmulatedHttpRequestAsync(context);
                });
            }
        }
#endif

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

                request.RequestId = requestId;

                this.CompletionSource = new TaskCompletionSource<ProxyReply>();
                this.RequestId        = requestId;
                this.Request          = request;
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

                CompletionSource.TrySetResult(reply);
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
                CompletionSource.TrySetCanceled();
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

                CompletionSource.TrySetException(e);
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static readonly object      staticSyncLock = new object();
        private static readonly Assembly    thisAssembly   = Assembly.GetExecutingAssembly();
        private static readonly INeonLogger log            = LogManager.Default.GetLogger<CadenceClient>();
        private static bool                 proxyWritten   = false;

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
                if (!proxyWritten)
                {
                    // Extract and decompress the [cadence-proxy] binary.  Note that it's
                    // possible that another instance of an .NET application using this 
                    // library is already runing on this machine such that the proxy
                    // binary file will be read-only.  In this case, we'll log and otherwise
                    // ignore the exception and assume that the proxy binary is correct.

                    try
                    {
                        var resourceStream = thisAssembly.GetManifestResourceStream(resourcePath);

                        if (resourceStream == null)
                        {
                            throw new KeyNotFoundException($"Embedded resource [{resourcePath}] not found.  Cannot launch [cadency-proxy].");
                        }

                        using (resourceStream)
                        {
                            using (var binaryStream = new FileStream(binaryPath, FileMode.Create, FileAccess.ReadWrite))
                            {
                                resourceStream.GunzipTo(binaryStream);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (File.Exists(binaryPath))
                        {
                            log.LogWarn($"[cadence-proxy] binary [{binaryPath}] already exists and is probably read-only.", e);
                        }
                        else
                        {
                            log.LogWarn($"[cadence-proxy] binary [{binaryPath}] cannot be written.", e);
                        }
                    }

                    if (NeonHelper.IsLinux || NeonHelper.IsOSX)
                    {
                        // We need to set the execute permissions on this file.  We're
                        // going to assume that only the root and current user will
                        // need to execute rights to the proxy binary.

                        var result = NeonHelper.ExecuteCapture("chmod", new object[] { "774", binaryPath });

                        if (result.ExitCode != 0)
                        {
                            throw new IOException($"Cannot set execute permissions for [{binaryPath}]:\r\n{result.ErrorText}");
                        }
                    }

                    proxyWritten = true;
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

        /// <summary>
        /// Establishes a connection to a Cadence cluster.
        /// </summary>
        /// <param name="settings">The <see cref="CadenceSettings"/>.</param>
        /// <returns>The connected <see cref="CadenceClient"/>.</returns>
        public static async Task<CadenceClient> ConnectAsync(CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            if (settings.ClientTimeout <= TimeSpan.Zero)
            {
                settings.ClientTimeout = TimeSpan.FromSeconds(30);
            }

            var client = new CadenceClient(settings);

            await client.SetWorkflowCacheSize(10000);

            return client;
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                          syncLock      = new object();
        private IPAddress                       address       = IPAddress.Parse("127.0.0.2");    // Using a non-default loopback to avoid port conflicts
        private Dictionary<long, Operation>     operations    = new Dictionary<long, Operation>();
        private Dictionary<long, Worker>        workers       = new Dictionary<long, Worker>();
        private long                            nextRequestId = 0;
        private int                             proxyPort;
        private HttpClient                      proxyClient;
        private IWebHost                        host;
        private Exception                       pendingException;
        private bool                            closingConnection;
        private bool                            connectionClosedRaised;
        private int                             workflowCacheSize;
        private Thread                          heartbeatThread;
        private Thread                          timeoutThread;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="CadenceSettings"/>.</param>
        private CadenceClient(CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            this.Settings = settings;

            if (settings.Servers == null || settings.Servers.Count == 0)
            {
                throw new CadenceConnectException("No Cadence servers were specified.");
            }

            foreach (var server in settings.Servers)
            {
                try
                {
                    if (server == null || !new Uri(server).IsAbsoluteUri)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    throw new CadenceConnectException($"Invalid Cadence server URI: {server}");
                }
            }

            if (settings.DebugIgnoreTimeouts)
            {
                // Use a really long HTTP timeout when timeout detection is disabled
                // to avoid having operations cancelled out from under us while we're
                // debugging this code.
                //
                // This should never happen for production.

                Settings.ProxyTimeout     =
                Settings.DebugHttpTimeout = TimeSpan.FromHours(48);
            }

            // Start the web server that will listen for requests from the associated 
            // [cadence-proxy] process.

            host = new WebHostBuilder()
                .UseKestrel(
                    options =>
                    {
                        options.Listen(address, !settings.DebugPrelaunched ? settings.ListenPort : debugClientPort);
                    })
                .ConfigureServices(
                    services =>
                    {
                        services.AddSingleton(typeof(CadenceClient), this);
                        services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);
                    })
                .UseStartup<Startup>()
                .Build();

            host.Start();

            ListenUri = new Uri(host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.OfType<string>().FirstOrDefault());

            // Determine the port we'll have [cadence-proxy] listen on and then
            // fire up the cadence-proxy process or the stubbed host.

            proxyPort = !settings.DebugPrelaunched ? NetHelper.GetUnusedTcpPort(address) : debugProxyPort;

#if DEBUG
            if (!settings.DebugEmulateProxy)
            {
                if (!Settings.DebugPrelaunched)
                {
                    ProxyProcess = StartProxy(new IPEndPoint(address, proxyPort), settings);
                }
            }
            else
            {
                // Start up a partially implemented emulation of a cadence-proxy.

                emulatedHost = new WebHostBuilder()
                    .UseKestrel(
                        options =>
                        {
                            options.Listen(address, proxyPort);
                        })
                    .ConfigureServices(
                        services =>
                        {
                            services.AddSingleton(typeof(CadenceClient), this);
                            services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);
                        })
                    .UseStartup<EmulatedStartup>()
                    .Build();

                emulatedHost.Start();
            }
#else
            ProxyProcess = StartProxy(new IPEndPoint(address, proxyPort), settings);
#endif

            // Create the HTTP client we'll use to communicate with the [cadence-proxy].

            var httpHandler = new HttpClientHandler()
            {
                // Disable compression because all communication is happening on
                // a loopback interface (essentially in-memory) so there's not
                // much point in taking the CPU hit to manage compression.

                AutomaticDecompression = DecompressionMethods.None
            };

            proxyClient = new HttpClient(httpHandler, disposeHandler: true)
            {
                BaseAddress = new Uri($"http://{address}:{proxyPort}"),
                Timeout     = settings.ProxyTimeout > TimeSpan.Zero ? settings.ProxyTimeout : Settings.DebugHttpTimeout
            };

            // Initilize the [cadence-proxy].

            if (!Settings.DebugDisableHandshakes)
            {
                try
                {
                    // Send the [InitializeRequest] to the [cadence-proxy] so it will know
                    // where to send reply messages.

                    var initializeRequest =
                        new InitializeRequest()
                        {
                            LibraryAddress = ListenUri.Host,
                            LibraryPort    = ListenUri.Port
                        };

                    CallProxyAsync(initializeRequest).Wait();

                    // Send the [ConnectRequest] to the [cadence-proxy] telling it
                    // how to connect to the Cadence cluster.

                    var sbEndpoints = new StringBuilder();

                    foreach (var serverUri in settings.Servers)
                    {
                        var uri = new Uri(serverUri);

                        sbEndpoints.AppendWithSeparator($"{uri.Host}:7933", ",");
                    }

                    var connectRequest = 
                        new ConnectRequest()
                        {
                            Endpoints = sbEndpoints.ToString(),
                            Identity  = settings.ClientIdentity
                        };


                    CallProxyAsync(connectRequest).Result.ThrowOnError();
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new CadenceConnectException("Cannot connect to Cadence cluster.", e);
                }
            }

            // Crank up the background threads which will handle [cadence-proxy]
            // health heartbeats as well as request timeouts.

            heartbeatThread = new Thread(new ThreadStart(HeartbeatThread));
            heartbeatThread.Start();

            timeoutThread = new Thread(new ThreadStart(TimeoutThread));
            timeoutThread.Start();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~CadenceClient()
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
            RaiseConnectionClosed();

            closingConnection = true;

            if (!Settings.DebugDisableHandshakes)
            {
                try
                {
                    // Gracefully stop all workflow workers.

                    List<Worker> workerList;

                    lock (syncLock)
                    {
                        workerList = workers.Values.ToList();
                    }

                    foreach (var worker in workerList)
                    {
                        StopWorkerAsync(worker).Wait();
                    }

                    // Signal the proxy that it should exit gracefully and then
                    // allow it [Settings.TerminateTimeout] to actually exit
                    // before killing it.
       
                    try
                    {
                        CallProxyAsync(new TerminateRequest(), timeout: Settings.DebugHttpTimeout).Wait();
                    }
                    catch
                    {
                        // Ignoring these.
                    }

                    if (ProxyProcess != null && !ProxyProcess.WaitForExit((int)Settings.TerminateTimeout.TotalMilliseconds))
                    {
                        log.LogWarn(() => $"[cadence-proxy]: Did not terminate gracefully within [{Settings.TerminateTimeout}].  Killing it now.");
                        ProxyProcess.Kill();
                    }

                    ProxyProcess = null;
                }
                catch
                {
                    // Ignoring this.
                }
            }

            if (heartbeatThread != null)
            {
                heartbeatThread.Join();
                heartbeatThread = null;
            }

            if (timeoutThread != null)
            {
                timeoutThread.Join();
                timeoutThread = null;
            }

#if DEBUG
            if (emulatedHost != null)
            {
                emulatedHost.Dispose();
                emulatedHost = null;
            }

            if (EmulatedLibraryClient != null)
            {
                EmulatedLibraryClient.Dispose();
                EmulatedLibraryClient = null;
            }
#endif

            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }

            if (host != null)
            {
                host.Dispose();
                host = null;
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
        public Uri ProxyUri => new Uri($"http://{address}:{proxyPort}");

        /// <summary>
        /// Returns the <b>cadence-proxy</b> process or <c>null</c>.s
        /// </summary>
        internal Process ProxyProcess { get; private set; }

        /// <summary>
        /// Raised when the connection is closed.  You can determine whether the connection
        /// was closed normally or due to an error by examining the <see cref="CadenceClientClosedArgs"/>
        /// arguments passed to the handler.
        /// </summary>
        public event CadenceClientClosedDelegate ConnectionClosed;

        /// <summary>
        /// Raises the <see cref="ConnectionClosed"/> event if it hasn't already
        /// been raised.
        /// </summary>
        /// <param name="exception">Optional exception to be included in the event.</param>
        private void RaiseConnectionClosed(Exception exception = null)
        {
            var raiseConnectionClosed = false;

            lock (syncLock)
            {
                raiseConnectionClosed  = !connectionClosedRaised;
                connectionClosedRaised = true;
            }

            if (!raiseConnectionClosed)
            {
            }

            if (raiseConnectionClosed)
            {
                ConnectionClosed?.Invoke(this, new CadenceClientClosedArgs() { Exception = exception });
            }

            // Signal the background threads that they need to exit.

            closingConnection = true;
        }

        /// <summary>
        /// Called when an HTTP request is received by the integrated web server 
        /// (presumably sent by the associated <b>cadence-proxy</b> process).
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
#if DEBUG
                    case "/echo":

                        await OnEchoRequestAsync(context);
                        break;
#endif
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
            var httpRequest  = context.Request;
            var httpResponse = context.Response;
            var proxyMessage = ProxyMessage.Deserialize<ProxyMessage>(httpRequest.Body);
            var request      = proxyMessage as ProxyRequest;
            var reply        = proxyMessage as ProxyReply;

            if (request != null)
            {
                // [cadence-proxy] has sent us a request.

                switch (request.Type)
                {
                    case MessageTypes.HeartbeatRequest:

                        await OnHeartbeatRequest((HeartbeatRequest)request);
                        break;

                    default:

                        httpResponse.StatusCode = StatusCodes.Status400BadRequest;
                        await httpResponse.WriteAsync($"[cadence-client] Does not support [{request.Type}] messages from the [cadence-proxy].");
                        break;
                }
            }
            else if (reply != null)
            {
                // [cadence-proxy] sent a reply to a request from the library.

                Operation operation;

                lock (syncLock)
                {
                    operations.TryGetValue(reply.RequestId, out operation);
                }

                if (operation != null)
                {
                    if (reply.Type != operation.Request.ReplyType)
                    {
                        httpResponse.StatusCode = StatusCodes.Status400BadRequest;
                        await httpResponse.WriteAsync($"[cadence-client] has a request [type={operation.Request.Type}, requestId={operation.RequestId}] pending but reply [type={reply.Type}] is not valid and will be ignored.");
                    }
                    else
                    {
                        operation.SetReply(reply);
                        httpResponse.StatusCode = StatusCodes.Status200OK;
                    }
                }
                else
                {
                    log.LogWarn(() => $"Reply [type={reply.Type}, requestId={reply.RequestId}] does not map to a pending operation and will be ignored.");

                    httpResponse.StatusCode = StatusCodes.Status400BadRequest;
                    await httpResponse.WriteAsync($"[cadence-client] does not have a pending operation with [requestId={reply.RequestId}].");
                }
            }
            else
            {
                // We should never see this.

                Covenant.Assert(false);
            }

            await Task.CompletedTask;
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
            try
            {
                var requestId = Interlocked.Increment(ref this.nextRequestId);
                var operation = new Operation(requestId, request, timeout);

                lock (syncLock)
                {
                    operations.Add(requestId, operation);
                }

                var response = await proxyClient.SendRequestAsync(request);

                response.EnsureSuccessStatusCode();

                return await operation.CompletionSource.Task;
            }
            catch (Exception e)
            {
                if (closingConnection && (request is HeartbeatRequest))
                {
                    // Special-case heartbeat replies while we're closing
                    // the connection to make things more deterministic.

                    return new HeartbeatReply() { RequestId = request.RequestId };
                }

                // We should never see an exception under normal circumstances.
                // Either a requestID somehow got reused (which should never 
                // happen) or the HTTP request to the [cadence-proxy] failed
                // to be transmitted, timed out, or the proxy returned an
                // error status code.
                //
                // We're going to save the exception to [pendingException]
                // and signal the background thread to close the connection.

                pendingException  = e;
                closingConnection = true;

                log.LogCritical(e);
                throw;
            }
        }

        /// <summary>
        /// <para>
        /// Asynchronously replies to a request from the <b>cadence-proxy</b>.
        /// </para>
        /// <note>
        /// The reply message's <see cref="ProxyReply.RequestId"/> will be automatically
        /// set to the <paramref name="request"/> message's request ID by this method.
        /// </note>
        /// </summary>
        /// <param name="request">The received request message.</param>
        /// <param name="reply">The reply message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ProxyReplyAsync(ProxyRequest request, ProxyReply reply)
        {
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentNullException>(reply != null);

            try
            {
                await proxyClient.SendReplyAsync(request, reply);
            }
            catch (Exception e)
            {
                // We should never see an exception under normal circumstances.
                // Either a requestID somehow got reused (which should never 
                // happen) or the HTTP request to the [cadence-proxy] failed
                // to be transmitted, timed out, or the proxy returned an
                // error status code.
                //
                // We're going to save the exception to [pendingException]
                // and signal the background thread to close the connection.

                pendingException  = e;
                closingConnection = true;

                log.LogCritical(e);
                throw;
            }
        }

        /// <summary>
        /// Implements the connection's background thread which is responsible
        /// for checking <b>cadence-proxy</b> health via heartbeat requests.
        /// </summary>
        private void HeartbeatThread()
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

                            if (!Settings.DebugDisableHeartbeats)
                            {
                                // Verify [cadence-proxy] health via by sending a heartbeat
                                // and waiting a bit for a reply.

                                try
                                {
                                    var heartbeatReply = await CallProxyAsync(new HeartbeatRequest(), timeout: Settings.DebugHttpTimeout);

                                    if (heartbeatReply.Error != null)
                                    {
                                        throw new Exception($"[cadence-proxy]: Heartbeat returns [{heartbeatReply.Error}].");
                                    }
                                }
                                catch (Exception e)
                                {
                                    log.LogError("Heartbeat check failed.  Closing Cadence connection.", e);
                                    exception = new CadenceTimeoutException("Heartbeat check failed.", e);

                                    // Break out of the while loop so we'll signal the application that
                                    // the connection has closed and then exit the thread below.

                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // We shouldn't see any exceptions here except perhaps
                        // [TaskCanceledException] when the connection is in
                        // the process of being closed.

                        if (!closingConnection || !e.Contains<TaskCanceledException>())
                        {
                            exception = e;
                            log.LogError(e);
                        }
                    }

                    if (exception == null && pendingException != null)
                    {
                        exception = pendingException;
                    }

                    // This is a good place to signal the client application that the
                    // connection has been closed.

                    RaiseConnectionClosed(exception);
                });
        }

        /// <summary>
        /// Implements the connection's background thread which is responsible
        /// for handling <b>cadence-proxy</b> request timeouts.
        /// </summary>
        private void TimeoutThread()
        {
            var sleepTime = TimeSpan.FromSeconds(1);
            var exception = (Exception)null;

            try
            {
                while (!closingConnection)
                {
                    Thread.Sleep(sleepTime);

                    // Look for any operations that have been running longer than
                    // the specified timeout and then individually cancel and
                    // remove them, and then notify the application that they were
                    // cancelled.

                    if (!Settings.DebugIgnoreTimeouts)
                    {
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

            if (exception == null && pendingException != null)
            {
                exception = pendingException;
            }

            // This is a good place to signal the client application that the
            // connection has been closed.

            RaiseConnectionClosed(exception);
        }

        //--------------------------------------------------------------------
        // These methods handle requests sent by the [cadence-proxy].

        /// <summary>
        /// Handles <see cref="HeartbeatRequest"/> messages received from the
        /// <b>cadence-proxy</b>.
        /// </summary>
        /// <param name="request">The received request.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnHeartbeatRequest(HeartbeatRequest request)
        {
            await ProxyReplyAsync(request, new HeartbeatReply());
        }
    }
}