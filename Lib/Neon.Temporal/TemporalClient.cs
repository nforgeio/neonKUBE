//-----------------------------------------------------------------------------
// FILE:	    TemporalClient.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
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
using Microsoft.Net.Http.Server;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Retry;
using Neon.Tasks;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Implements a client that will be connected to a Temporal cluster and be used
    /// to create and manage workflows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To get started with Temporal, you'll need to deploy a Temporal cluster with
    /// one or more nodes and the establish a connection to the cluster from your
    /// workflow/activity implementations and management tools.  This is pretty
    /// easy to do.
    /// </para>
    /// <para>
    /// Temporal clusters interacts with client APIs via gRPC on <b>port 7233</b> (by default).
    /// Server nodes are typically configured behind a TCP load balancer or a DNS
    /// name is configured such that it returns the IP addresses for each server
    /// node.  The <see cref="TemporalSettings.HostPort"/> property is used to 
    /// specify how to connect to the cluster.  For single node clusters or clusters
    /// behind a load balancer, you'll typically specify <b>HOST:7233</b> where
    /// <b>HOST</b> is the DNS name or IP address for the node.
    /// </para>
    /// <para>
    /// Alternatively, if you've configured DNS to return IP addresses for each cluster node,
    /// you can specify <b>dns:///host:port</b>.  In this case, the client will 
    /// round-robin between the node addresses returned to locate healthy nodes
    /// to communicate with.
    /// </para>
    /// <para>
    /// To establish a connection, you'll construct a <see cref="TemporalSettings"/>
    /// and add your cluster endpoint to the <see cref="TemporalSettings.HostPort"/>
    /// and then call the static <see cref="TemporalClient.ConnectAsync(TemporalSettings)"/>
    /// method to obtain a connected <see cref="TemporalClient"/>.  You'll use this
    /// for registering workflows and activities types as well as the workers that
    /// indicate that workflows and activities can be executed in the current process.
    /// </para>
    /// <note>
    /// <b>IMPORTANT:</b> The current .NET Temporal client release supports having only
    /// one client open at a time.  A <see cref="NotSupportedException"/> will be thrown
    /// when attempting to connect a second client.  This restriction may be relaxed
    /// for future releases.
    /// </note>
    /// <para>
    /// You'll implement your workflows and activities by implementing classes that
    /// derive from <see cref="WorkflowBase"/> and <see cref="ActivityBase"/> and then
    /// registering these types with Temporal.  Then you'll start workflow or activity
    /// workers so that Temporal will begin scheduling operations for execution by your code.
    /// Workflows and activities are registered using the fully qualified names 
    /// of the derived <see cref="WorkflowBase"/> and <see cref="ActivityBase"/> types
    /// by defaut, but you can customize this if desired.
    /// </para>
    /// <para>
    /// Temporal supports the concept of <b>namespaces</b> and task lists.  Temporal is multi-tenant and
    /// namespaces are simply string names used to isolate deployments.  For example, you could
    /// define a <b>PROD</b> namespace for your production workflows and <b>STAGE</b> for preproduction.
    /// It's also often useful to organize namespaces by the teams implementing and managing their
    /// workflows.  Each <see cref="TemporalClient"/> is assigned to a default namespace via
    /// <see cref="TemporalSettings.Namespace"/> and this defaults to <b>"default"</b> unless you
    /// specify something different when connecting the client.
    /// </para>
    /// <note>
    /// You can override the default client namespace for specific operations via <see cref="WorkerOptions"/>,
    /// <see cref="WorkflowOptions"/>, <see cref="ChildWorkflowOptions"/>, <see cref="ActivityOptions"/> as
    /// well as via the <see cref="WorkflowInterfaceAttribute"/>, <see cref="WorkflowMethodAttribute"/>,
    /// <see cref="ActivityInterfaceAttribute"/> and <see cref="ActivityMethodAttribute"/> attributes
    /// decorating your workflow and activity interface definitions.
    /// </note>
    /// <para>
    /// Once you have a connected <see cref="TemporalClient"/>, you can create and manage Temporal namespaces
    /// via methods like <see cref="RegisterNamespaceAsync(string, string, string, int, bool)"/>,
    /// <see cref="DescribeNamespaceAsync(string)"/>, and <see cref="UpdateNamespaceAsync(string, UpdateNamespaceRequest)"/>.
    /// </para>
    /// <para>
    /// Workflows and activities are implemented by creating a <see cref="Worker"/> via <see cref="NewWorkerAsync(WorkerOptions)"/>,
    /// registering your workflow and activity implementations with the worker and then starting
    /// the worker via <see cref="Worker.StartAsync"/>.  Temporal will begin scheduling pending
    /// workflows and activities on workers after they've been started.
    /// </para>
    /// <para>
    /// The <see cref="Worker"/> class provides methods for registering individual workflow and
    /// activity implementations, including <see cref="Worker.RegisterWorkflowAsync{TWorkflow}(string, string)"/> 
    /// and <see cref="Worker.RegisterActivityAsync{TActivity}(string, string)"/> as well as methods
    /// that register all workflow and activity implementation discovered in an assembly:
    /// <see cref="Worker.RegisterAssemblyActivitiesAsync(Assembly, string)"/>,
    /// <see cref="Worker.RegisterAssemblyWorkflowsAsync(Assembly, string)"/>, and
    /// <see cref="Worker.RegisterAssemblyAsync(Assembly, string)"/> (which registers
    /// both workflow and assembly implementations).
    /// </para>
    /// <para>
    /// Workflows are implemented by defining an interface describing the workflow methods
    /// and then writing a class the implements your interface and also inherits <see cref="WorkflowBase"/>.  
    /// Your workflow interface  must define at least one entry point method tagged by <see cref="WorkflowMethodAttribute"/> and
    /// may optionally include signal and query methods  tagged by <see cref="SignalMethodAttribute"/> 
    /// and <see cref="QueryMethodAttribute"/>.
    /// </para>
    /// <para>
    /// Activities are implemented in the same way by defining an activity interface and then writing a class
    /// that implements this  interface. and inherits <see cref="ActivityBase"/>.  Your activity interface
    /// must define at least one entry point method.
    /// </para>
    /// <para>
    /// You'll generally create stub classes to start and manage workflows and activities.
    /// These come in various flavors with the most important being typed and untyped stubs.
    /// Typed stubs are nice because they implement your workflow or activity interface so
    /// that the C# compiler can provide compile-time type checking.  Untyped stubs provide
    /// a way to interact with workflows and activities written on other languages or for
    /// which you don't have source code.
    /// </para>
    /// <para>
    /// You can create typed external workflow stubs via <see cref="NewWorkflowStub{TWorkflowInterface}(string, string, string)"/>
    /// and <see cref="NewWorkflowStub{TWorkflowInterface}(WorkflowOptions, string)"/>.
    /// </para>
    /// <para>
    /// Workflows can use their <see cref="Workflow"/> property to create child workflow as
    /// well as activity stubs.
    /// </para>
    /// <para><b>Task Lists</b></para>
    /// <para>
    /// Task lists are used by Temporal to identify the set of workflows and activities that
    /// are implemented by workers.  For example, if you deploy a program called <b>payments.exe</b>
    /// that implements payment related workflows and activities like <b>validate</b>,
    /// <b>debit</b>, <b>credit</b>,... you could register these and then start a worker using
    /// <b>tasklist=payments</b>.
    /// </para>
    /// <para>
    /// You'll need to provide the correct task list when executing a workflow or normal (non-local)
    /// activity.  Temporal will schedule the workflow or activity on one of the workers that
    /// was started with the specified task list.  The most convienent way to specify the task list
    /// is to tag your workflow and activity interfaces with <c>[WorkflowInterface(TaskList = "payments")]</c>
    /// and <c>[ActivityInterface(TaskList = "payments")]</c> attributes, specifying the target task list.
    /// </para>
    /// <note>
    /// You may specify a default task list when connecting a <see cref="TemporalClient"/> via
    /// <see cref="TemporalSettings.DefaultTaskList"/> (which defaults to <c>null</c>).  This may
    /// be convienent for simple deployments.
    /// </note>
    /// <para>
    /// You may also specify a custom task list in the workflow and activity options used when
    /// executing a workflow or activity.  A task list specified in one of these options takes
    /// precedence over the task list specified in an attribute.
    /// </para>
    /// <note>
    /// The .NET client will complain if a task list is not specified in either an interface
    /// attribute via options and the there's no client default task list set.
    /// </note>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> You need to take care to ensure that the task lists you use for your
    /// workers uniquely identify the set of workflows and activities implemented by your the workers.
    /// For example, if you start two workers, <b>worker-a</b> and <b>worker-b</b> using the same
    /// task list, but <b>worker-a</b> registers the <b>foo</b> workflow and <b>worker-b</b>
    /// registers the <c>bar</c> activity, you're going run into trouble.
    /// </para>
    /// <para>
    /// The problem is that Cadence assumes that both workers implement the same workflows, both
    /// <b>foo</b> and <b>bar</b> in this case.  Say you start a <b>foo</b> workflow.  Temporal
    /// will select one of <b>worker-a</b> or <b>worker-b</b> to run the workflow.  If Temporal
    /// happens to select <b>worker-a</b> everything will work as expected because <b>foo</b>
    /// is registered there.  If Temporal selects <b>worker-b</b> the initial execution will fail
    /// because <b>foo</b> is not registered there.  Temporal handles this a decision task failure
    /// and will attempt to reschedule the workflow on another worker (hopefully <b>worker-a</b>
    /// this time).
    /// </para>
    /// <para>
    /// Temporal fault tolerance will probably end up being able to handle this sort misconfiguration,
    /// but at the cost of additional delays as well as unnecessary communication overhead to workers
    /// that will never be able to execute unregistered workflows and activities.
    /// </para>
    /// <para>
    /// So the moral of this store is carefully choose your task lists to match the set of workflows
    /// and activities implemented by your application.  One common approach is to name the task list
    /// after the service or application that implements the workflow anbd activities.
    /// </para>
    /// </note>
    /// </remarks>
    public partial class TemporalClient : IDisposable
    {
        /// <summary>
        /// The <b>temporal-proxy</b> listening port to use when <see cref="TemporalSettings.DebugPrelaunched"/>
        /// mode is enabled.
        /// </summary>
        private const int debugProxyPort = 5000;

        /// <summary>
        /// The <b>temporal-client</b> listening port to use when <see cref="TemporalSettings.DebugPrelaunched"/>
        /// mode is enabled.
        /// </summary>
        private const int debugClientPort = 5001;

        /// <summary>
        /// The signal name used to retrieve the current stack trace.
        /// </summary>
        internal const string QueryStack = "__stack_trace";

        /// <summary>
        /// The internal query name used to poll the state of a synchronous signals.
        /// </summary>
        internal const string QuerySyncSignal = "@@neon-query-sync-signal";

        /// <summary>
        /// The signal name used for synchronous signals.  Signals sent here will be
        /// handled internally by <see cref="WorkflowBase"/> and forwarded on to the
        /// user's signal handler method.
        /// </summary>
        internal const string SignalSync = "@@neon-sync-signal";

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Configures the <b>temporal-client</b> connection's web server used to 
        /// receive messages from the <b>temporal-proxy</b> when serving via
        /// Kestrel on .NET Core.
        /// </summary>
        private class Startup
        {
            public void Configure(IApplicationBuilder app)
            {
                app.Run(async context =>
                {
                    await SyncContext.ClearAsync;
                    await OnKestralRequestAsync(context);
                });
            }
        }

        /// <summary>
        /// Used for tracking pending <b>temporal-proxy</b> operations.
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
                Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

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
                Covenant.Requires<ArgumentNullException>(reply != null, nameof(reply));

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
                Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

                CompletionSource.TrySetException(e);
            }
        }

        /// <summary>
        /// Used to specify the HTTP reply to be returned for a received HTTP request.
        /// </summary>
        private struct HttpReply
        {
            /// <summary>
            /// The response HTTP status code.
            /// </summary>
            public int StatusCode;

            /// <summary>
            /// The response error message or <c>null</c>.
            /// </summary>
            public string Message;
        }

        /// <summary>
        /// Implements a simple integrated HTTP server that works for both .NET Core
        /// as well as .NET Framework, using Kestrel for .NET Core and WebListener
        /// for .NET Framework.
        /// </summary>
        private class HttpServer : IDisposable
        {
            private IWebHost    kestrel;    // Used for .NET Core
            private WebListener listener;   // Used for .NET Framework

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="address">The IP address where the service should listen.</param>
            /// <param name="settings">The Temporal settings.</param>
            public HttpServer(IPAddress address, TemporalSettings settings)
            {
                switch (NeonHelper.Framework)
                {
                    case NetFramework.Core:

                        InitializeNetCore(address, settings);
                        break;

                    case NetFramework.Framework:

                        InitializeNetFramework(address, settings);
                        break;

                    default:

                        throw new NotSupportedException($"Unsupported .NET framework: {NeonHelper.Framework}");
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                switch (NeonHelper.Framework)
                {
                    case NetFramework.Core:

                        CoreDispose();
                        break;

                    case NetFramework.Framework:

                        NetDispose();
                        break;

                    default:

                        throw new NotSupportedException($"Unsupported .NET framework: {NeonHelper.Framework}");
                }
            }

            /// <summary>
            /// Disposes the .NET Core implementation.
            /// </summary>
            private void CoreDispose()
            {
                if (kestrel != null)
                {
                    kestrel.Dispose();
                    kestrel = null;
                }
            }

            /// <summary>
            /// Dispose the .NET Framework implementation.
            /// </summary>
            private void NetDispose()
            {
                if (listener != null)
                {
                    listener.Dispose();
                    listener = null;
                }
            }

            /// <summary>
            /// Returns the URI where the server is listening.
            /// </summary>
            public Uri ListenUri { get; private set; }

            /// <summary>
            /// Initializes the HTTP server when running on .NET Core.
            /// </summary>
            /// <param name="address">The IP address where the service should listen.</param>
            /// <param name="settings">The Temporal settings.</param>
            private void InitializeNetCore(IPAddress address, TemporalSettings settings)
            {
                if (kestrel == null)
                {
                    // Start the web server that will listen for requests from the associated 
                    // [temporal-proxy] process.

                    kestrel = new WebHostBuilder()
                        .UseKestrel(
                            options =>
                            {
                                options.Limits.MaxRequestBodySize = null;     // Disables request size limits
                                options.Listen(address, !settings.DebugPrelaunched ? settings.ListenPort : debugClientPort);
                            })
                        .ConfigureServices(
                            services =>
                            {
                                services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);
                            })
                        .UseStartup<Startup>()
                        .Build();

                    kestrel.Start();

                    ListenUri = new Uri(kestrel.ServerFeatures.Get<IServerAddressesFeature>().Addresses.OfType<string>().FirstOrDefault());
                }
            }

            /// <summary>
            /// Initializes the HTTP server when running on .NET Framework.
            /// </summary>
            /// <param name="address">The IP address where the service should listen.</param>
            /// <param name="settings">The Temporal settings.</param>
            private void InitializeNetFramework(IPAddress address, TemporalSettings settings)
            {
                var openPort         = NetHelper.GetUnusedTcpPort(address);
                var listenerSettings = new WebListenerSettings();

                ListenUri = new Uri($"http://{address}:{openPort}");

                listenerSettings.UrlPrefixes.Add(ListenUri.ToString());

                listener = new WebListener(listenerSettings);
                listener.Start();

                // Process the inbound messages on a free running task.

                _ = Task.Run(
                    async () =>
                    {
                        while (true)
                        {
                            try
                            {
                                var newContext = await listener.AcceptAsync();

                                // Process each request in its own task.

                                _ = Task.Factory.StartNew(
                                    async (object arg) =>
                                    {
                                        await SyncContext.ClearAsync;

                                        using (var context = (RequestContext)arg)
                                        {
                                            await OnListenerRequestAsync(context);
                                        }
                                    },
                                    newContext);
                            }
                            catch
                            {
                                // We're going to see exceptions like ObjectDisposedException when
                                // the listener is disposed.  We're just going to ignore these
                                // and exit.

                                break;
                            }
                        }
                    });
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static readonly object                  syncLock      = new object();
        private static readonly Assembly                thisAssembly  = Assembly.GetExecutingAssembly();
        private static readonly INeonLogger             log           = LogManager.Default.GetLogger<TemporalClient>();
        private static bool                             proxyWritten  = false;
        private static long                             nextClientId  = 0;
        private static Dictionary<long, TemporalClient> idToClient    = new Dictionary<long, TemporalClient>();
        private static long                             nextRequestId = 0;
        private static Dictionary<long, Operation>      operations    = new Dictionary<long, Operation>();
        private static INeonLogger                      temporalLogger;
        private static INeonLogger                      temporalProxyLogger;

        /// <summary>
        /// Resets <see cref="TemporalClient"/> to its initial state, by closing
        /// any existing connections and clearing any operation state.  This is
        /// called by the <b>TemporalFixture</b>.
        /// </summary>
        internal static void Reset()
        {
            foreach (var client in idToClient.Values.ToArray())
            {
                client.Dispose();
            }

            List<TemporalClient> clients;

            lock (syncLock)
            {
                clients = idToClient.Values.ToList();
            }

            foreach (var client in clients)
            {
                client.Dispose();
            }

            lock (syncLock)
            {
                idToClient.Clear();
                operations.Clear();
            }
        }

        /// <summary>
        /// <para>
        /// Writes the <b>temporal-proxy</b> binaries to the specified folder.  This is
        /// provided so that you can pre-provision the executable and then use the 
        /// <see cref="TemporalSettings.BinaryPath"/> setting to reference it.
        /// These files will be written:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>temporal-proxy.win.exe</b></term>
        ///     <description>
        ///     The Windows AMD64 executable
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>temporal-proxy.linux</b></term>
        ///     <description>
        ///     The Linux AMD64 executable
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>temporal-proxy.osx</b></term>
        ///     <description>
        ///     The OS/X AMD64 executable
        ///     </description>
        /// </item>
        /// </list>
        /// <para>
        /// This is useful for situations where the executable must be pre-provisioned for
        /// security.  One example is deploying Temporal workers to a Docker container with
        /// a read-only file system.
        /// </para>
        /// </summary>
        /// <param name="folderPath">Path to the output folder.</param>
        public static void ExtractTemporalProxy(string folderPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(folderPath));

            var resources = new string[]
            {
                "Neon.Temporal.Resources.temporal-proxy.win.exe.gz",
                "Neon.Temporal.Resources.temporal-proxy.linux.gz",
                "Neon.Temporal.Resources.temporal-proxy.osx.gz"
            };

            var files = new string[]
            {
                "temporal-proxy.win.exe",
                "temporal-proxy.linux",
                "temporal-proxy.osx"
            };

            for (int i = 0; i < resources.Length; i++)
            {
                var resourcePath   = resources[i];
                var resourceStream = thisAssembly.GetManifestResourceStream(resourcePath);
                var binaryPath     = Path.Combine(folderPath, files[i]);

                if (resourceStream == null)
                {
                    throw new KeyNotFoundException($"Embedded resource [{resourcePath}] not found.  Cannot extract [temporal-proxy].");
                }

                using (resourceStream)
                {
                    using (var binaryStream = new FileStream(binaryPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        resourceStream.GunzipTo(binaryStream);
                    }
                }
            }
        } 

        /// <summary>
        /// <para>
        /// Writes the correct <b>temporal-proxy</b> binary for the current environment
        /// to the file system (if that hasn't been done already) and then launches 
        /// a proxy instance configured to listen at the specified endpoint.
        /// </para>
        /// <note>
        /// If <see cref="TemporalSettings.BinaryPath"/> is not <c>null</c> or empty then
        /// we'll just execute that binary rather than trying to extract one.  We'll also
        /// assume that we already have execute permissions.
        /// </note>
        /// </summary>
        /// <param name="endpoint">The network endpoint where the proxy will listen.</param>
        /// <param name="settings">The Temporal connection settings.</param>
        /// <param name="clientId">The associated client ID.</param>
        /// <returns>The proxy <see cref="Process"/>.</returns>
        /// <remarks>
        /// By default, this class will write the binary to the same directory where
        /// this assembly resides.  This should work for most circumstances.  On the
        /// odd change that the current application doesn't have write access to this
        /// directory, you may specify an alternative via <paramref name="settings"/>.
        /// </remarks>
        private static Process StartProxy(IPEndPoint endpoint, TemporalSettings settings, long clientId)
        {
            Covenant.Requires<ArgumentNullException>(endpoint != null, nameof(endpoint));
            Covenant.Requires<ArgumentNullException>(settings != null, nameof(settings));

            var binaryPath = settings.BinaryPath;

            if (string.IsNullOrEmpty(binaryPath))
            {
                var binaryFolder = settings.BinaryFolder;

                if (string.IsNullOrEmpty(binaryFolder))
                {
                    binaryFolder = NeonHelper.GetAssemblyFolder(thisAssembly);
                }

                string resourcePath;

                if (NeonHelper.IsWindows)
                {
                    resourcePath = "Neon.Temporal.Resources.temporal-proxy.win.exe.gz";
                    binaryPath   = Path.Combine(binaryFolder, "temporal-proxy.exe");
                }
                else if (NeonHelper.IsOSX)
                {
                    resourcePath = "Neon.Temporal.Resources.temporal-proxy.osx.gz";
                    binaryPath = Path.Combine(binaryFolder, "temporal-proxy");
                }
                else if (NeonHelper.IsLinux)
                {
                    resourcePath = "Neon.Temporal.Resources.temporal-proxy.linux.gz";
                    binaryPath = Path.Combine(binaryFolder, "temporal-proxy");
                }
                else
                {
                    throw new NotImplementedException();
                }

                lock (syncLock)
                {
                    if (!proxyWritten)
                    {
                        // Extract and decompress the [temporal-proxy] binary.  Note that it's
                        // possible that another instance of an .NET application using this 
                        // library is already runing on this machine such that the proxy
                        // binary file will be read-only.  In this case, we'll log and otherwise
                        // ignore the exception and assume that the proxy binary is correct.

                        try
                        {
                            var resourceStream = thisAssembly.GetManifestResourceStream(resourcePath);

                            if (resourceStream == null)
                            {
                                throw new KeyNotFoundException($"Embedded resource [{resourcePath}] not found.  Cannot launch [temporal-proxy].");
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
                                log.LogWarn($"[temporal-proxy] binary [{binaryPath}] already exists and is probably read-only.", e);
                            }
                            else
                            {
                                log.LogWarn($"[temporal-proxy] binary [{binaryPath}] cannot be written.", e);
                            }
                        }

                        if (NeonHelper.IsLinux || NeonHelper.IsOSX)
                        {
                            // We need to set the execute permissions on this file.  We're
                            // going to assume that only the root and current user will
                            // need execute rights to the proxy binary.

                            var result = NeonHelper.ExecuteCapture("chmod", new object[] { "774", binaryPath });

                            if (result.ExitCode != 0)
                            {
                                throw new IOException($"Cannot set execute permissions for [{binaryPath}]:\r\n{result.ErrorText}");
                            }
                        }

                        proxyWritten = true;
                    }
                }
            }

            // Launch the proxy with a console window when we're running in DEBUG
            // mode on Windows.  We'll ignore this for the other platforms.

            var debugOption = settings.Debug ? " --debug" : string.Empty;
            var commandLine = $"--listen {endpoint.Address}:{endpoint.Port}{debugOption} --client-id {clientId}";
            
            if (NeonHelper.IsWindows && settings.Debug)
            {
                var startInfo = new ProcessStartInfo(binaryPath, commandLine)
                {
                    UseShellExecute = true,
                };

                return Process.Start(startInfo);
            }
            else
            {
                var process = new Process();

                process.StartInfo.UseShellExecute        = false;
                process.StartInfo.FileName               = binaryPath;
                process.StartInfo.Arguments              = commandLine;
                process.StartInfo.RedirectStandardError  = true;
                process.StartInfo.RedirectStandardOutput = true;

                // These event handlers intentionally ignore the process output because
                // we don't want it to get mixed in with the application's output
                // streams which will often be used for streaming application log data
                // or for other purposes.
                //
                // [temporal-proxy] is already transmitting log information to the
                // client and the client is directing that to the normal logging
                // mechanisms.

                process.ErrorDataReceived  += (s, a) => { };
                process.OutputDataReceived += (s, a) => { };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                return process;
            }
        }

        /// <summary>
        /// Returns the client associated with a given ID.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <returns>The <see cref="TemporalClient"/> or <c>null</c> if the client doesn't exist.</returns>
        private static TemporalClient GetClient(long clientId)
        {
            lock (syncLock)
            {
                if (idToClient.TryGetValue(clientId, out var client))
                {
                    return client;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Establishes a connection to a Temporal cluster.
        /// </summary>
        /// <param name="settings">The <see cref="TemporalSettings"/>.</param>
        /// <returns>The connected <see cref="TemporalClient"/>.</returns>
        /// <remarks>
        /// <note>
        /// The <see cref="TemporalSettings"/> passed must specify a <see cref="TemporalSettings.Namespace"/>.
        /// </note>
        /// </remarks>
        public static async Task<TemporalClient> ConnectAsync(TemporalSettings settings)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(settings != null, nameof(settings));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(settings.Namespace), nameof(settings.Namespace), "You must specifiy a non-empty Temporal namespace in settings.");

            settings = settings.Clone();    // Configure the client using cloned settings

            var client = new TemporalClient(settings);

            settings.Client = client;       // This makes the settings read-only

            // Initialize the [temporal-proxy].

            if (!settings.DebugDisableHandshakes)
            {
                try
                {
                    // We're going to wait up to 30 seconds for [temporal-proxy] to initialize
                    // itself to be ready to receive requests.  We're going to ping the proxy's
                    // HTTP endpoint with GET requests until we see an HTTP response.
                    //
                    // Note that [temporal-proxy] accepts only POST requests, so these GETs will
                    // be ignored and we'll see a 405 Method Not Allowed response when the proxy
                    // is ready.

                    using (var initClient = new HttpClient())
                    {
                        initClient.BaseAddress = client.proxyClient.BaseAddress;
                        initClient.Timeout     = TimeSpan.FromSeconds(1);

                        await NeonHelper.WaitForAsync(
                            async () =>
                            {
                                try
                                {
                                    await initClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"));

                                    return true;
                                }
                                catch
                                {
                                    return false;
                                }
                            },
                            timeout: TimeSpan.FromSeconds(30),
                            pollTime: TimeSpan.FromMilliseconds(500));
                    }

                    // Send the [InitializeRequest] to the [temporal-proxy] so it will know
                    // where the .NET Client is listening.

                    var initializeRequest =
                        new InitializeRequest()
                        {
                            LibraryAddress = client.ListenUri.Host,
                            LibraryPort    = client.ListenUri.Port,
                            LogLevel       = client.Settings.ProxyLogLevel
                        };

                    await client.CallProxyAsync(initializeRequest);

                    // Send the [ConnectRequest] to the [temporal-proxy] telling it
                    // how to connect to the Temporal cluster.

                    var connectRequest =
                        new ConnectRequest()
                        {
                            HostPort        = settings.HostPort,
                            Identity        = settings.ClientIdentity,
                            ClientTimeout   = TimeSpan.FromSeconds(30),
                            Namespace       = settings.Namespace,
                            CreateNamespace = settings.CreateNamespace
                        };

                    client.CallProxyAsync(connectRequest).Result.ThrowOnError();
                }
                catch (Exception e)
                {
                    client.Dispose();
                    throw new ConnectException("Cannot connect to Temporal cluster.", e);
                }
            }

            // Crank up the background threads which will handle [temporal-proxy]
            // request timeouts.

            client.heartbeatThread = new Thread(new ThreadStart(client.HeartbeatThread));
            client.heartbeatThread.Start();

            client.timeoutThread = new Thread(new ThreadStart(client.TimeoutThread));
            client.timeoutThread.Start();

            // Initialize the cache size to a known value.

            try
            {
                await client.SetCacheMaximumSizeAsync(10000);
            }
            catch
            {
                // Ignoring these.
            }

            return client;
        }

        /// <summary>
        /// Called when an HTTP request is received by the integrated Kestrel web server 
        /// (presumably sent by the associated <b>temporal-proxy</b> process).
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task OnKestralRequestAsync(HttpContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            if (request.Method != "PUT")
            {
                response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await response.WriteAsync($"[{request.Method}] HTTP method is not supported.  All requests must be submitted via [PUT].");
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

                        // $hack(jefflill):
                        //
                        // We need to receive the entire request body before deserializing the
                        // the message because BinaryReader doesn't seem to play nice with reading
                        // from the body stream.  We're seeing EndOfStream exceptions when we try
                        // to read more than about 64KiB bytes of data which is the default size
                        // of the Kestrel receive buffer.  This suggests that there's some kind
                        // of problem reading the next buffer from the request socket.

                        var bodyStream = MemoryStreamPool.Alloc();

                        try
                        {
                            await request.Body.CopyToAsync(bodyStream);

                            bodyStream.Position = 0;

                            var proxyMessage = ProxyMessage.Deserialize<ProxyMessage>(bodyStream);
                            var httpReply    = await OnRootRequestAsync(proxyMessage);

                            response.StatusCode = httpReply.StatusCode;

                            if (!string.IsNullOrEmpty(httpReply.Message))
                            {
                                await response.WriteAsync(httpReply.Message);
                            }
                        }
                        finally
                        {
                            MemoryStreamPool.Free(bodyStream);
                        }
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
        /// Called when an HTTP request is received by the integrated WebListener
        /// (presumably sent by the associated <b>temporal-proxy</b> process).
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task OnListenerRequestAsync(RequestContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            if (request.Method != "PUT")
            {
                response.StatusCode = StatusCodes.Status405MethodNotAllowed;

                await response.Body.WriteAsync(Encoding.UTF8.GetBytes($"[{request.Method}] HTTP method is not supported.  All requests must be submitted via [PUT]."));
                return;
            }

            if (request.ContentType != ProxyMessage.ContentType)
            {
                response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await response.Body.WriteAsync(Encoding.UTF8.GetBytes($"[{request.ContentType}] Content-Type is not supported.  All requests must be submitted with [Content-Type={request.ContentType}]."));
                return;
            }

            try
            {
                switch (request.Path)
                {
                    case "/":

                        // $hack(jefflill):
                        //
                        // We need to receive the entire request body before deserializing the
                        // the message because BinaryReader doesn't seem to play nice with reading
                        // from the body stream.  We're seeing EndOfStream exceptions when we try
                        // to read more than about 64KiB bytes of data which is the default size
                        // of the Kestrel receive buffer.  This suggests that there's some kind
                        // of problem reading the next buffer from the request socket.
                        //
                        // This isn't a huge issue since we're going to convert temporal-proxy into
                        // a shared library where we'll be passing message buffers directly.

                        var bodyStream = MemoryStreamPool.Alloc();

                        try
                        {
                            await request.Body.CopyToAsync(bodyStream);

                            bodyStream.Position = 0;

                            var proxyMessage = ProxyMessage.Deserialize<ProxyMessage>(bodyStream);
                            var httpReply    = await OnRootRequestAsync(proxyMessage);

                            response.StatusCode = httpReply.StatusCode;

                            if (!string.IsNullOrEmpty(httpReply.Message))
                            {
                                await response.Body.WriteAsync(Encoding.UTF8.GetBytes(httpReply.Message));
                            }
                        }
                        finally
                        {
                            MemoryStreamPool.Free(bodyStream);
                        }
                        break;

                    default:

                        response.StatusCode = StatusCodes.Status404NotFound;
                        await response.Body.WriteAsync(Encoding.UTF8.GetBytes($"[{request.Path}] HTTP PATH is not supported.  Only [/] and [/echo] are allowed."));
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
        /// <param name="proxyMessage">The received message.</param>
        /// <returns>The HTTP reply information.</returns>
        private static async Task<HttpReply> OnRootRequestAsync(ProxyMessage proxyMessage)
        {
            Covenant.Requires<ArgumentNullException>(proxyMessage != null, nameof(proxyMessage));

            var httpReply = new HttpReply() { StatusCode = StatusCodes.Status200OK };
            var request   = proxyMessage as ProxyRequest;
            var reply     = proxyMessage as ProxyReply;
            var client    = GetClient(proxyMessage.ClientId);

            Covenant.Assert(client != null);

            if (request != null)
            {
                // [temporal-proxy] has sent us a request.  We're going to process this
                // in a detached task so we'll can return the HTTP response immediately
                // to the [temporal-proxy].

                _ = Task.Run(async () =>
                {
                    switch (request.Type)
                    {
                        case InternalMessageTypes.LogRequest:

                            await OnLogRequestAsync(client, request);
                            break;

                        case InternalMessageTypes.WorkflowInvokeRequest:
                        case InternalMessageTypes.WorkflowSignalInvokeRequest:
                        case InternalMessageTypes.WorkflowQueryInvokeRequest:
                        case InternalMessageTypes.ActivityInvokeLocalRequest:
                        case InternalMessageTypes.WorkflowFutureReadyRequest:

                            await WorkflowBase.OnProxyRequestAsync(client.GetWorkerById(request.WorkerId), request);
                            break;

                        case InternalMessageTypes.ActivityInvokeRequest:
                        case InternalMessageTypes.ActivityStoppingRequest:

                            await client.GetWorkerById(request.WorkerId).OnProxyRequestAsync(request);
                            break;

                        default:

                            httpReply.StatusCode = StatusCodes.Status400BadRequest;
                            httpReply.Message = $"[temporal-client] does not support [{request.Type}] messages from the [temporal-proxy].";
                            break;
                    }
                });
            }
            else if (reply != null)
            {
                // [temporal-proxy] sent a reply to a request from the client.

                Operation operation;

                lock (syncLock)
                {
                    operations.TryGetValue(reply.RequestId, out operation);
                    operations.Remove(reply.RequestId);
                }

                if (operation != null)
                {
                    if (reply.Type != operation.Request.ReplyType)
                    {
                        httpReply.StatusCode = StatusCodes.Status400BadRequest;
                        httpReply.Message    = $"[temporal-client] has a request [type={operation.Request.Type}, requestId={operation.RequestId}] pending but reply [type={reply.Type}] is not valid and will be ignored.";
                    }
                    else
                    {
                        operation.SetReply(reply);
                    }
                }
                else
                {
                    log.LogWarn(() => $"Reply [type={reply.Type}, requestId={reply.RequestId}] does not map to a pending operation and will be ignored.");

                    httpReply.StatusCode = StatusCodes.Status400BadRequest;
                    httpReply.Message    = $"[temporal-client] does not have a pending operation with [requestId={reply.RequestId}].";
                }
            }
            else
            {
                // We should never see this.

                Covenant.Assert(false);
            }

            return await Task.FromResult(httpReply);
        }

        private static async Task OnLogRequestAsync(TemporalClient client, ProxyRequest request)
        {
            var logRequest = (LogRequest)request;

            if (logRequest.FromTemporal)
            {
                switch (logRequest.LogLevel)
                {
                    default:
                    case Neon.Diagnostics.LogLevel.None:

                        break;  // NOP

                    case Neon.Diagnostics.LogLevel.Critical:

                        temporalLogger.LogCritical(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SError:

                        temporalLogger.LogSError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Error:

                        temporalLogger.LogError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Warn:

                        temporalLogger.LogWarn(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Info:

                        temporalLogger.LogInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SInfo:

                        temporalLogger.LogSInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Debug:

                        temporalLogger.LogDebug(logRequest.LogMessage);
                        break;
                }
            }
            else
            {
                switch (logRequest.LogLevel)
                {
                    default:
                    case Neon.Diagnostics.LogLevel.None:

                        break;  // NOP

                    case Neon.Diagnostics.LogLevel.Critical:

                        temporalProxyLogger.LogCritical(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SError:

                        temporalProxyLogger.LogSError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Error:

                        temporalProxyLogger.LogError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Warn:

                        temporalProxyLogger.LogWarn(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Info:

                        temporalProxyLogger.LogInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SInfo:

                        temporalProxyLogger.LogSInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Debug:

                        temporalProxyLogger.LogDebug(logRequest.LogMessage);
                        break;
                }
            }

            await client.ProxyReplyAsync(request, new LogReply());
        }

        //---------------------------------------------------------------------
        // Instance members

        private Process                     proxyProcess = null;
        private int                         proxyPort    = 0;
        private Dictionary<long, Worker>    idToWorker   = new Dictionary<long, Worker>();
        private bool                        isDisposed   = false;
        private HttpClient                  proxyClient;
        private HttpServer                  httpServer;
        private Exception                   pendingException;
        private bool                        closingConnection;
        private bool                        connectionClosedRaised;
        private int                         workflowCacheSize;
        private Thread                      heartbeatThread;
        private Thread                      timeoutThread;
        private IRetryPolicy                syncSignalRetry;

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        internal TemporalClient()
        {
            Settings = new TemporalSettings();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="TemporalSettings"/>.</param>
        private TemporalClient(TemporalSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null, nameof(settings));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.Namespace), nameof(settings));

            this.ClientId = Interlocked.Increment(ref nextClientId);
            this.Settings = settings;

            if (string.IsNullOrEmpty(settings.HostPort))
            {
                throw new ConnectException("No Temporal server was specified.");
            }

            if (settings.DebugIgnoreTimeouts)
            {
                // Use a really long HTTP timeout when timeout detection is disabled
                // to avoid having operations cancelled out from under us while we're
                // debugging this code.
                //
                // This should never happen for production.

                Settings.DebugHttpTimeout    = TimeSpan.FromHours(48);
                Settings.ProxyTimeoutSeconds = Settings.DebugHttpTimeout.TotalSeconds;
            }

            DataConverter       = new JsonDataConverter();
            temporalLogger      = LogManager.Default.GetLogger("temporal", isLogEnabledFunc: () => Settings.LogTemporal);
            temporalProxyLogger = LogManager.Default.GetLogger("temporal-proxy", isLogEnabledFunc: () => Settings.LogTemporalProxy);

            // Initialize a reasonable default synchronous signal query retry policy. 

            syncSignalRetry = new ExponentialRetryPolicy(
                e => true, 
                maxAttempts:          int.MaxValue, 
                initialRetryInterval: TimeSpan.FromSeconds(0.25), 
                maxRetryInterval:     TimeSpan.FromSeconds(1), 
                timeout:              TimeSpan.FromSeconds(60), 
                sourceModule:         nameof(TemporalClient));

            lock (syncLock)
            {
                try
                {
                    idToClient.Add(this.ClientId, this);

                    httpServer = new HttpServer(Address, settings);
                    ListenUri  = httpServer.ListenUri;

                    // Determine the port we'll have [temporal-proxy] listen on and then
                    // fire up the temporal-proxy process.

                    proxyPort = !settings.DebugPrelaunched ? NetHelper.GetUnusedTcpPort(Address) : debugProxyPort;

                    if (!Settings.DebugPrelaunched && proxyProcess == null)
                    {
                        proxyProcess = StartProxy(new IPEndPoint(Address, proxyPort), settings, ClientId);
                    }
                }
                catch
                {
                    idToClient.Remove(this.ClientId);
                    throw;
                }
            }

            // Create the HTTP client we'll use to communicate with the [temporal-proxy].

            var httpHandler = new HttpClientHandler()
            {
                // Disable compression because all communication is happening on
                // a loopback interface (essentially in-memory) so there's not
                // much point in taking the CPU hit to manage compression.

                AutomaticDecompression = DecompressionMethods.None
            };

            proxyClient = new HttpClient(httpHandler, disposeHandler: true)
            {
                BaseAddress = new Uri($"http://{Address}:{proxyPort}"),
                Timeout     = settings.ProxyTimeout > TimeSpan.Zero ? settings.ProxyTimeout : Settings.DebugHttpTimeout
            };
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TemporalClient()
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

            if (Settings != null && !Settings.DebugDisableHandshakes)
            {
                try
                {
                    // Gracefully stop all workflow workers.

                    List<Worker> workerList;

                    lock (syncLock)
                    {
                        workerList = idToWorker.Values.ToList();
                    }

                    foreach (var worker in workerList)
                    {
                        worker.Dispose();
                    }

                    // Signal the proxy to disconnect and then terminate.

                    CallProxyAsync(new DisconnectRequest()).Wait();

                    // Terminate the associated proxy.

                    lock (syncLock)
                    {
                        if (isDisposed)
                        {
                            return;
                        }
                        else
                        {
                            isDisposed = true;

                            lock (syncLock)
                            {
                                idToClient.Remove(ClientId);
                            }
                        }

                        if (proxyProcess != null)
                        {
                            // The [DisconnectRequest] sent above should have gracefully disconnected
                            // from the Temporal cluster so we can just kill the temporal-proxy process.
                            // There's no reason to send a [TerminateRequest] anymore.

                            proxyProcess.Kill();
                            proxyProcess = null;
                        }

                        if (httpServer != null)
                        {
                            httpServer.Dispose();
                            httpServer = null;
                        }
                    }
                }
                catch
                {
                    // Ignoring errors.
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

            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }

            RaiseConnectionClosed();
        }

        /// <summary>
        /// Returns the IP address to be used for binding the network interface for both
        /// the local web server as well as that for <b>temporal-proxy</b>.
        /// </summary>
        private IPAddress Address => IPAddress.Loopback;

        /// <summary>
        /// Returns the locally unique ID for the client instance.
        /// </summary>
        internal long ClientId { get; private set; }

        /// <summary>
        /// Returns the settings used to create the client.
        /// </summary>
        public TemporalSettings Settings { get; private set; }

        /// <summary>
        /// Returns the URI the client is listening on for requests from the <b>temporal-proxy</b>.
        /// </summary>
        public Uri ListenUri { get; private set; }

        /// <summary>
        /// Returns the URI the associated <b>temporal-proxy</b> instance is listening on.
        /// </summary>
        public Uri ProxyUri => new Uri($"http://{Address}:{proxyPort}");

        /// <summary>
        /// <para>
        /// Specifies the <see cref="IDataConverter"/> used for workflows and activities managed by the client.
        /// This defaults to <see cref="JsonDataConverter"/>.
        /// </para>
        /// <note>
        /// When you need a custom data converter, you must set this immediately after connecting
        /// the client.  You must not change the converter after you've started workers.
        /// </note>
        /// </summary>
        public IDataConverter DataConverter { get; set; }

        /// <summary>
        /// Raised when the connection is closed.  You can determine whether the connection
        /// was closed normally or due to an error by examining the <see cref="TemporalClientClosedArgs"/>
        /// arguments passed to the handler.
        /// </summary>
        public event TemporalClosedDelegate ConnectionClosed;

        /// <summary>
        /// <para>
        /// <b>INTERNAL USE ONLY:</b> Appends a line of text to the debug log which is
        /// used internally to debug generated code like stubs.  This hardcodes its
        /// output to <b>C:\Temp\temporal-debug.log</b> so this currently only works
        /// on Windows.
        /// </para>
        /// <note>
        /// This method doesn't actually log anything unless <see cref="TemporalSettings.Debug"/>
        /// is set to <c>true</c>.
        /// </note>
        /// </summary>
        /// <param name="text">The line of text to be written.</param>
        public void DebugLog(string text)
        {
            if (Settings.Debug)
            {
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("----"))
                {
                    TemporalHelper.DebugLog($"clientId:{ClientId} {text}");
                }
                else
                {
                    TemporalHelper.DebugLog(text);
                }
            }
        }

        /// <summary>
        /// <para>
        /// Controls how synchronous signals operations are polled until the signal operation is
        /// completed.  This defaults to something reasonable.
        /// </para>
        /// <note>
        /// The transient detector function specified by the policy passed is ignore and is
        /// replaced by a function that considers all exceptions to be transient.
        /// </note>
        /// </summary>
        public IRetryPolicy SyncSignalRetry
        {
            get => syncSignalRetry;

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null, nameof(value));

                syncSignalRetry = value.Clone(e => true);
            }
        }

        /// <summary>
        /// Ensures that that client instance is not disposed.
        /// </summary>
        /// <param name="noClosingCheck">Optionally skip check that the connection is in the process of closing.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the client is disposed or is no longer connected to <b>temporal-proxy</b>.</exception>
        internal void EnsureNotDisposed(bool noClosingCheck = false)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(TemporalClient));
            }
            else if (!noClosingCheck && closingConnection)
            {
                throw new ObjectDisposedException(nameof(TemporalClient), "Connection to [temporal-proxy] is closing.");
            }
        }

        /// <summary>
        /// Raises the <see cref="ConnectionClosed"/> event if it hasn't been
        /// raised already.
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

            if (raiseConnectionClosed)
            {
                ConnectionClosed?.Invoke(this, new TemporalClientClosedArgs() { Exception = exception });
            }

            // Signal the background threads that they need to exit.

            closingConnection = true;
        }

        /// <summary>
        /// Looks up an associated worker by its ID.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <returns>The <see cref="Worker"/> or <c>null</c>.</returns>
        internal Worker GetWorkerById(long workerId)
        {
            lock (syncLock)
            {
                if (idToWorker.TryGetValue(workerId, out var worker))
                {
                    return worker;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the Temporal namespace to be referenced for an operation.  If <paramref name="namespace"/>
        /// is not <c>null</c> or empty then that will be returned otherwise the  <see cref="TemporalSettings.Namespace"/>
        /// will be returned.  Note that one of <paramref name="namespace"/> or the default namespace must
        /// be non-empty.
        /// </summary>
        /// <param name="namespace">The specific namespace to use or null/empty.</param>
        /// <returns>The namespace to be referenced.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="namespace"/> and the default namespace are both null or empty.</exception>
        internal string ResolveNamespace(string @namespace)
        {
            EnsureNotDisposed();

            if (!string.IsNullOrEmpty(@namespace))
            {
                return @namespace;
            }
            else if (!string.IsNullOrEmpty(Settings.Namespace))
            {
                return Settings.Namespace;
            }

            throw new ArgumentNullException(nameof(@namespace),$"One of [{nameof(@namespace)}] parameter or the client's default namespace (specified as [{nameof(TemporalClient)}.{nameof(TemporalClient.Settings)}.{nameof(TemporalSettings.Namespace)}]) must be non-empty.");
        }

        /// <summary>
        /// Asynchronously calls the <b>temporal-proxy</b> by sending a request message
        /// and then waits for a reply.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">
        /// Optionally specifies the maximum time to wait for the operation to complete.
        /// This defaults to unlimited.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The reply message.</returns>
        internal async Task<ProxyReply> CallProxyAsync(ProxyRequest request, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            request.ClientId = this.ClientId;

            try
            {
                var requestId = Interlocked.Increment(ref nextRequestId);
                var operation = new Operation(requestId, request, timeout);

                lock (syncLock)
                {
                    operations.Add(requestId, operation);
                }

                if (cancellationToken != default)
                {
                    request.IsCancellable = true;

                    cancellationToken.Register(
                        async () =>
                        {
                            await CallProxyAsync(new CancelRequest() { RequestId = requestId });
                        });
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
                // happen) or the HTTP request to the [temporal-proxy] failed
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
        /// Asynchronously replies to a request from the <b>temporal-proxy</b>.
        /// </para>
        /// <note>
        /// The reply message's <see cref="ProxyReply.RequestId"/> will be automatically
        /// set to the <paramref name="request"/> message's request ID by this method.
        /// </note>
        /// </summary>
        /// <param name="request">The received request message.</param>
        /// <param name="reply">The reply message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal async Task ProxyReplyAsync(ProxyRequest request, ProxyReply reply)
        {
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));
            Covenant.Requires<ArgumentNullException>(reply != null, nameof(reply));

            reply.ClientId = ClientId;

            try
            {
                await proxyClient.SendReplyAsync(request, reply);
            }
            catch (Exception e)
            {
                // We're going to ignore exceptions for [LogReply] messages.

                if (reply.Type == InternalMessageTypes.LogReply)
                {
                    return;
                }

                // Otherwise, should never see an exception under normal circumstances.
                // Either a requestID somehow got reused (which should never 
                // happen) the HTTP request to the [temporal-proxy] failed
                // to be transmitted, timed out, or the proxy returned an
                // error status code, or maybe the client was closed out 
                // from under us.
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
        /// for checking <b>temporal-proxy</b> health via heartbeat requests.
        /// </summary>
        private void HeartbeatThread()
        {
            Task.Run(
                async () =>
                {
                    var sleepTime = Settings.HeartbeatInterval;
                    var exception = (Exception)null;

                    try
                    {
                        while (!closingConnection)
                        {
                            Thread.Sleep(sleepTime);

                            if (!Settings.DebugDisableHeartbeats)
                            {
                                // Verify [temporal-proxy] health by sending a heartbeat
                                // and waiting a bit for a reply.

                                try
                                {
                                    var heartbeatReply = await CallProxyAsync(new HeartbeatRequest(), timeout: Settings.HeartbeatTimeout);

                                    if (heartbeatReply.Error != null && !closingConnection)
                                    {
                                        throw new Exception($"[temporal-proxy]: Heartbeat returns [{heartbeatReply.Error}].");
                                    }
                                }
                                catch (Exception e)
                                {
                                    log.LogError("Heartbeat check failed.  Closing Temporal connection.", e);

                                    exception = new TimeoutException("[temporal-proxy] heartbeat failure.", e);

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
        /// for handling <b>temporal-proxy</b> request timeouts.
        /// </summary>
        private void TimeoutThread()
        {
            var sleepTime = TimeSpan.FromSeconds(1);
            var exception = (Exception)null;

            try
            {
                while (!closingConnection && !isDisposed)
                {
                    Thread.Sleep(sleepTime);

                    // Look for any operations that have been running longer than
                    // the specified timeout and then individually cancel and
                    // remove them, and then notify the application that they 
                    // were cancelled.

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
                            // Send a cancel to the [temporal-proxy] for each timed-out
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

                            log.LogWarn(() => $"Request Timeout: [request={operation.Request.GetType().Name}, started={operation.StartTimeUtc.ToString(NeonHelper.DateFormat100NsTZ)}, timeout={operation.Timeout}].");

                            // $todo(jefflill):
                            //
                            // We're not supporting cancellation so I'm going to comment
                            // this out.  We should probably remove this if we decide that
                            // we're never going to support this.
#if TODO
                            var notAwaitingThis = Task.Run(
                                async () =>
                                {
                                    if (operation.Request.Type != InternalMessageTypes.CancelRequest)
                                    {
                                        await CallProxyAsync(new CancelRequest() { TargetRequestId = operation.RequestId }, timeout: TimeSpan.FromSeconds(1));
                                    }

                                    operation.SetCanceled();
                                });
#endif
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
    }
}