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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Server;

using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements a client that will be connected to a Cadence cluster and be used
    /// to create and manage workflows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To get started with Cadence, you'll need to deploy a Cadence cluster with
    /// one or more nodes and the establish a connection to the cluster from your
    /// workflow/activity implementations and management tools.  This is pretty
    /// easy to do.
    /// </para>
    /// <para>
    /// First, you'll need to know the URI of at least one of the Cadence cluster
    /// nodes.  Cadence listens on port <b>79133</b> by default so you cluster URIs
    /// will typically look like: <b>http://CADENCE-NODE:7933</b>.
    /// </para>
    /// <note>
    /// For production clusters with multiple Cadence nodes, you should specify
    /// multiple URIs when connecting just in case the one of the nodes may be
    /// offline for some reason.
    /// </note>
    /// <para>
    /// To establish a connection, you'll construct a <see cref="CadenceSettings"/>
    /// and add your node URIs to the <see cref="CadenceSettings.Servers"/> list
    /// and then call the static <see cref="CadenceClient.ConnectAsync(CadenceSettings)"/>
    /// method to obtain a connected <see cref="CadenceClient"/>.  You'll use this
    /// for registering workflows and activities types as well as the workers that
    /// indicate that workflows and activities can be executed in the current process.
    /// </para>
    /// <note>
    /// <b>IMPORTANT:</b> The current .NET Cadence client release supports having only
    /// one client open at a time.  A <see cref="NotSupportedException"/> will be thrown
    /// when attempting to connect a second client.  This restriction may be relaxed
    /// for future releases.
    /// </note>
    /// <para>
    /// You'll implement your workflows and activities by implementing classes that
    /// derive from <see cref="WorkflowBase"/> and <see cref="ActivityBase"/> and then
    /// registering these types with Cadence.  Then you'll start workflow or activity
    /// workers so that Cadence will begin scheduling operations for execution by your code.
    /// Workflows and activities are registered using the fully qualified names 
    /// of the derived <see cref="WorkflowBase"/> and <see cref="ActivityBase"/> types
    /// by defaut, but you can customize this if desired.
    /// </para>
    /// <para>
    /// Cadence supports the concept of domains and task lists.  Domains and task lists are
    /// used to organize workflows and activities.  Workflows and activities essentially 
    /// reside in a registered domain, which is essentially just a namespace specified by
    /// a string.  The combination of a domain along with a workflow or activity type name
    /// must be unique within a Cadence cluster.  Once you have a connected <see cref="CadenceClient"/>,
    /// you can create and manage Cadence domains via methods like <see cref="RegisterDomainAsync(string, string, string, int, bool)"/>,
    /// <see cref="DescribeDomainAsync(string)"/>, and <see cref="UpdateDomainAsync(string, UpdateDomainRequest)"/>.
    /// Domains can be used provide isolated areas for different teams and/or different environments
    /// (e.g. production, staging, and test).  We discuss task lists in detail further below.
    /// </para>
    /// <para>
    /// Cadence workers are started to indicate that the current process can execute workflows
    /// and activities from a Cadence domain, and optionally a task list (discussed further below).
    /// You'll call <see cref="StartWorkerAsync(string, WorkerOptions, string)"/> to indicate
    /// that Cadence can begin scheduling workflow and activity executions from the current client.
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
    /// After establishing a connection ot a Cadence cluster, you'll need to call 
    /// <see cref="CadenceClient.RegisterWorkflowAsync{TWorkflowInterface}(string, string)"/> and/or
    /// <see cref="CadenceClient.RegisterActivityAsync{TActivity}(string, string)"/> to register your
    /// workflow and activity implementations with Cadence.  These calls combined with the
    /// workers described above determine which workflows and activities may be scheduled
    /// on the current client/process.
    /// </para>
    /// <para>
    /// For situations where you have a lot of workflow and activity classes, it can become
    /// cumbersome to register each implementation class individually (generally because you
    /// forget to register new classes after they've been implemented).  To assist with this,
    /// you can also tag your workflow and activity classes with <see cref="WorkflowAttribute"/>
    /// or <see cref="ActivityAttribute"/> with <see cref="WorkflowAttribute.AutoRegister"/>
    /// or <see cref="ActivityAttribute.AutoRegister"/> set to <c>true</c> and then call
    /// <see cref="CadenceClient.RegisterAssemblyWorkflowsAsync(Assembly, string)"/> and/or
    /// <see cref="CadenceClient.RegisterAssemblyActivitiesAsync(Assembly, string)"/> to scan an
    /// assembly and automatically register the tagged implementation classes it finds.
    /// </para>
    /// <note>
    /// <para>
    /// The .NET client uses a simple huristic to try to ensure that the default workflow and activity
    /// type names applied when the <see cref="WorkflowAttribute.Name"/> and <see cref="ActivityAttribute.Name"/>
    /// properties are not set for the interface and implementation classes.  If the interface
    /// name starts with an "I", the "I" will be stripped out before generating the fully qualified
    /// type name.  This handles the common C# convention where interface names started with an "I"
    /// and the implementing class uses the same name as the interface, but without the "I".
    /// </para>
    /// <para>
    /// If this huristic doesn't work, you'll need to explicitly specify the same type name in
    /// the <see cref="WorkflowAttribute"/> or <see cref="ActivityAttribute"/> tagging your 
    /// interface and class definitions.
    /// </para>
    /// </note>
    /// <para>
    /// Next you'll need to start workflow and/or activity workers.  These indicate to Cadence that 
    /// the current process implements specific workflow and activity types.  You'll call
    /// <see cref="StartWorkerAsync(string, WorkerOptions, string)"/>.  You can customize the
    /// Cadence domain and task list the worker will listen on as well as whether activities,
    /// workflows, or both are to be processed.
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
    /// Task lists provide an additional way to customize where workflows and activities are executed.
    /// A task list is simply a string used in addition to the domain to indicate which workflows and
    /// activities will be scheduled for execution by workers.  For external workflows,
    /// you can specify a default task list via <see cref="CadenceSettings.DefaultTaskList"/>.  
    /// Any non-empty custom string is allowed for task lists.  Child workflow and activity task lists
    /// will default to the parent workflow's task list by default.
    /// </para>
    /// <para>
    /// Task lists are typically only required for somewhat advanced deployments.  Let's go through
    /// an example to see how this works.  Imagine that you're a movie studio that needs to render
    /// an animated movie with Cadence.  You've implemented a workflow that breaks the movie up into
    /// 5 minute segments and then schedules an activity to render each segment.  Now assume that 
    /// we have two kinds of servers, one just a basic general purpose server and the other that
    /// includes high-end GPUs that are required for rendering.  In the simple case, you'd like
    /// the workflows to execute on the regular server and the activites to run on the GPU machines
    /// (because there's no point in wasting any expensive GPU machine resources on the workflow).
    /// </para>
    /// <para>
    /// This scenario can addressed by having the application running on the regular machines
    /// call <see cref="StartWorkerAsync(string, WorkerOptions, string)"/> with <see cref="WorkerOptions.DisableActivityWorker"/><c>=true</c>
    /// and the application running on the GPU servers call this with with <see cref="WorkerOptions.DisableWorkflowWorker"/><c>=true</c>.
    /// Both could specify the domain as <b>"render"</b> and set  task list as <b>"all"</b>
    /// (or something).  With this setup, workflows will be scheduled on the regular machines 
    /// and activities on the GPU machines.
    /// </para>
    /// <para>
    /// Now imagine a more complex scenario where we need to render two movies on the cluster at 
    /// the same time and we'd like to dedicate two thirds of our GPU machines to <b>movie1</b> and
    /// the other third to <b>movie2</b>.  This can be accomplished via task lists:
    /// </para>
    /// <para>
    /// We'd start by defining a task list for each movie: <b>"movie1"</b> and <b>"movie2"</b> and
    /// then call <see cref="StartWorkerAsync(string, WorkerOptions, string)"/> with <see cref="WorkerOptions.DisableActivityWorker"/><c>=true</c>
    /// twice on the regular machines and once for each task list.  This will schedule workflows for each movie
    /// on these machines (this is OK for this scenario because the workflow won't consume many
    /// resources).  Then on 2/3s of the GPU machines, we'll call <see cref="StartWorkerAsync(string, WorkerOptions, string)"/> 
    /// with <see cref="WorkerOptions.DisableWorkflowWorker"/><c>=true</c> with the <b>"movie1"</b>
    /// task list and the remaining one third of the GPU machines <b>"movie2"</b> as the task list. 
    /// Then we'll start the rendering workflow for the first movie specifying <b>"movie1"</b> as the
    /// task list and again for the second movie specifying <b>"movie2"</b>.
    /// </para>
    /// <para>
    /// The two movie workflows will be scheduled on the regular machines and these will each
    /// start the rendering activities using the <b>"movie1"</b> task list for the first movie
    /// and <b>"movie2"</b> for the second one and Cadence will then schedule these activities
    /// on the appropriate GPU servers.
    /// </para>
    /// <para>
    /// These are just a couple examples.  Domains, task lists, and worker options can be combined
    /// in different ways to manage where workflows and activities will be scheduled for execution.
    /// </para>
    /// </remarks>
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
        /// receive messages from the <b>cadence-proxy</b> when serving via
        /// Kestrel on .NET Core.
        /// </summary>
        private class Startup
        {
            public void Configure(IApplicationBuilder app)
            {
                app.Run(async context =>
                {
                    await OnKestralRequestAsync(context);
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
            /// <param name="settings">The Cadence settings.</param>
            public HttpServer(IPAddress address, CadenceSettings settings)
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
            /// <param name="settings">The Cadence settings.</param>
            private void InitializeNetCore(IPAddress address, CadenceSettings settings)
            {
                if (kestrel == null)
                {
                    // Start the web server that will listen for requests from the associated 
                    // [cadence-proxy] process.

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
            /// <param name="settings">The Cadence settings.</param>
            private void InitializeNetFramework(IPAddress address, CadenceSettings settings)
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
        private static readonly INeonLogger             log           = LogManager.Default.GetLogger<CadenceClient>();
        private static bool                             proxyWritten  = false;
        private static long                             nextClientId  = 0;
        private static Dictionary<long, CadenceClient>  idToClient    = new Dictionary<long, CadenceClient>();
        private static bool                             compilerReady = false;
        private static long                             nextRequestId = 0;
        private static Dictionary<long, Operation>      operations    = new Dictionary<long, Operation>();
        private static INeonLogger                      cadenceLogger;
        private static INeonLogger                      cadenceProxyLogger;

        /// <summary>
        /// Resets <see cref="CadenceClient"/> to its initial state, by closing
        /// and existing connections and clearing any operation state.  This is
        /// called by the <b>CadenceFixture</b>.
        /// </summary>
        internal static void Reset()
        {
            foreach (var client in idToClient.Values)
            {
                client.Dispose();
            }

            ActivityBase.Reset();
            WorkflowBase.Reset();

            lock (syncLock)
            {
                idToClient.Clear();
                operations.Clear();
            }
        }

        /// <summary>
        /// Writes the correct <b>cadence-proxy</b> binary for the current environment
        /// to the file system (if that hasn't been done already) and then launches 
        /// a proxy instance configured to listen at the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The network endpoint where the proxy will listen.</param>
        /// <param name="settings">The cadence connection settings.</param>
        /// <param name="clientId">The associated client ID.</param>
        /// <returns>The proxy <see cref="Process"/>.</returns>
        /// <remarks>
        /// By default, this class will write the binary to the same directory where
        /// this assembly resides.  This should work for most circumstances.  On the
        /// odd change that the current application doesn't have write access to this
        /// directory, you may specify an alternative via <paramref name="settings"/>.
        /// </remarks>
        private static Process StartProxy(IPEndPoint endpoint, CadenceSettings settings, long clientId)
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

            lock (syncLock)
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

            // Launch the proxy with a console window when we're running in DEBUG
            // mode on Windows.  We'll ignore this for the other platforms.

            var debugOption = settings.Debug ? " --debug" : string.Empty;
            var commandLine = $"--listen {endpoint.Address}:{endpoint.Port}{debugOption} --client-id {clientId}";

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
        /// Returns the client associated with a given ID.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <returns>The <see cref="CadenceClient"/> or <c>null</c> if the client doesn't exist.</returns>
        private static CadenceClient GetClient(long clientId)
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
        /// Establishes a connection to a Cadence cluster.
        /// </summary>
        /// <param name="settings">The <see cref="CadenceSettings"/>.</param>
        /// <returns>The connected <see cref="CadenceClient"/>.</returns>
        /// <remarks>
        /// <note>
        /// The <see cref="CadenceSettings"/> passed must specify a <see cref="CadenceSettings.DefaultDomain"/>.
        /// </note>
        /// </remarks>
        public static async Task<CadenceClient> ConnectAsync(CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.DefaultDomain), "You must specifiy a non-empty default Cadence domain.");

            InitializeCompiler();

            var client = new CadenceClient(settings);

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
        /// (presumably sent by the associated <b>cadence-proxy</b> process).
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

                        // $hack(jeff.lill):
                        //
                        // We need to receive the entire request body before deserializing the
                        // the message because BinaryReader doesn't seem to play nice with reading
                        // from the body stream.  We're seeing EndOfStream exceptions when we try
                        // to read more than about 64KiB bytes of data which is the default size
                        // of the Kestrel receive buffer.  This suggests that there's some kind
                        // of problem reading the next buffer from the request socket.
                        //
                        // This isn't a huge issue since we're going to convert cadence-proxy into
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
        /// (presumably sent by the associated <b>cadence-proxy</b> process).
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

                        // $hack(jeff.lill):
                        //
                        // We need to receive the entire request body before deserializing the
                        // the message because BinaryReader doesn't seem to play nice with reading
                        // from the body stream.  We're seeing EndOfStream exceptions when we try
                        // to read more than about 64KiB bytes of data which is the default size
                        // of the Kestrel receive buffer.  This suggests that there's some kind
                        // of problem reading the next buffer from the request socket.
                        //
                        // This isn't a huge issue since we're going to convert cadence-proxy into
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
            Covenant.Requires<ArgumentNullException>(proxyMessage != null);

            var httpReply = new HttpReply() { StatusCode = StatusCodes.Status200OK };
            var request   = proxyMessage as ProxyRequest;
            var reply     = proxyMessage as ProxyReply;
            var client    = GetClient(proxyMessage.ClientId);

            Covenant.Assert(client != null);

            if (request != null)
            {
                // [cadence-proxy] has sent us a request.

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

                        await WorkflowBase.OnProxyRequestAsync(client, request);
                        break;

                    case InternalMessageTypes.ActivityInvokeRequest:
                    case InternalMessageTypes.ActivityStoppingRequest:

                        await ActivityBase.OnProxyRequestAsync(client, request);
                        break;

                    default:

                        httpReply.StatusCode = StatusCodes.Status400BadRequest;
                        httpReply.Message    = $"[cadence-client] does not support [{request.Type}] messages from the [cadence-proxy].";
                        break;
                }
            }
            else if (reply != null)
            {
                // [cadence-proxy] sent a reply to a request from the client.

                Operation operation;

                lock (syncLock)
                {
                    operations.TryGetValue(reply.RequestId, out operation);
                }

                if (operation != null)
                {
                    if (reply.Type != operation.Request.ReplyType)
                    {
                        httpReply.StatusCode = StatusCodes.Status400BadRequest;
                        httpReply.Message    = $"[cadence-client] has a request [type={operation.Request.Type}, requestId={operation.RequestId}] pending but reply [type={reply.Type}] is not valid and will be ignored.";
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
                    httpReply.Message    = $"[cadence-client] does not have a pending operation with [requestId={reply.RequestId}].";
                }
            }
            else
            {
                // We should never see this.

                Covenant.Assert(false);
            }

            return httpReply;
        }

        private static async Task OnLogRequestAsync(CadenceClient client, ProxyRequest request)
        {
            var logRequest = (LogRequest)request;

            if (logRequest.FromCadence)
            {
                switch (logRequest.LogLevel)
                {
                    default:
                    case Neon.Diagnostics.LogLevel.None:

                        break;  // NOP

                    case Neon.Diagnostics.LogLevel.Critical:

                        cadenceLogger.LogCritical(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SError:

                        cadenceLogger.LogSError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Error:

                        cadenceLogger.LogError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Warn:

                        cadenceLogger.LogWarn(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Info:

                        cadenceLogger.LogInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SInfo:

                        cadenceLogger.LogSInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Debug:

                        cadenceLogger.LogDebug(logRequest.LogMessage);
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

                        cadenceProxyLogger.LogCritical(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SError:

                        cadenceProxyLogger.LogSError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Error:

                        cadenceProxyLogger.LogError(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Warn:

                        cadenceProxyLogger.LogWarn(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Info:

                        cadenceProxyLogger.LogInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.SInfo:

                        cadenceProxyLogger.LogSInfo(logRequest.LogMessage);
                        break;

                    case Neon.Diagnostics.LogLevel.Debug:

                        cadenceProxyLogger.LogDebug(logRequest.LogMessage);
                        break;
                }
            }

            await client.ProxyReplyAsync(request, new LogReply());
        }

        /// <summary>
        /// Ensures that the Microsoft C# compiler libraries are preloaded and ready
        /// so that subsequent complations won't take excessive time.
        /// </summary>
        private static void InitializeCompiler()
        {
            lock (syncLock)
            {
                // The .NET client dynamically generates code at runtime to implement
                // workflow stubs.  The Microsoft C# compiler classes take about 1.8
                // seconds to load and compile code for the first time.  Subsequent
                // compiles take about 200ms.
                //
                // The problem with this is that 1.8 seconds is quite long and is
                // roughly 1/5th of the default decision task timeout of 10 seconds.
                // So it's conceivable that this additional delay could push a
                // workflow to timeout.

                // $todo(jeff.lill):
                //
                // A potentially better approach would be to have the registrationd
                // methods prebuild (and cache) all of the stubs and/or implement
                // more specific stub generation methods.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/615

                const string source =
@"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence.WorkflowStub
{
    internal class __CompilerInitialized
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<int> DoNothingAsync()
        {
            // Call a few things so the the C# compiler will need to load some assemblies.

            await CadenceClient.ConnectAsync(new CadenceSettings());
            return await Task.FromResult(0);
        }
    }
}
";
                if (compilerReady)
                {
                    return;
                }

                var syntaxTree = CSharpSyntaxTree.ParseText(source);
                var references = new List<MetadataReference>();

                // Reference these required assemblies.

                references.Add(MetadataReference.CreateFromFile(typeof(NeonHelper).Assembly.Location));

                // Reference all loaded assemblies.

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }

                var assemblyName    = "Neon-Cadence-WorkflowStub-Initialize";
                var compilerOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);
                var compilation     = CSharpCompilation.Create(assemblyName, new[] { syntaxTree }, references, compilerOptions);
                var assemblyStream  = new MemoryStream();

                using (var pdbStream = new MemoryStream())
                {
                    var emitted = compilation.Emit(assemblyStream, pdbStream);

                    if (!emitted.Success)
                    {
                        throw new CompilerErrorException(emitted.Diagnostics);
                    }
                }

                assemblyStream.Position = 0;
                CadenceHelper.LoadAssembly(assemblyStream);

                compilerReady = true;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private IPAddress                       address       = IPAddress.Parse("127.0.0.2");    // Using a non-default loopback to avoid port conflicts
        private Process                         proxyProcess  = null;
        private int                             proxyPort     = 0;
        private Dictionary<long, Worker>        workers       = new Dictionary<long, Worker>();
        private Dictionary<string, Type>        activityTypes = new Dictionary<string, Type>();
        private bool                            isDisposed    = false;
        private HttpClient                      proxyClient;
        private HttpServer                      httpServer;
        private Exception                       pendingException;
        private bool                            closingConnection;
        private bool                            connectionClosedRaised;
        private int                             workflowCacheSize;
        private Thread                          heartbeatThread;
        private Thread                          timeoutThread;
        private bool                            workflowWorkerStarted;
        private bool                            activityWorkerStarted;

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        internal CadenceClient()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="CadenceSettings"/>.</param>
        private CadenceClient(CadenceSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.DefaultDomain));

            this.ClientId = Interlocked.Increment(ref nextClientId);
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

                Settings.DebugHttpTimeout    = TimeSpan.FromHours(48);
                Settings.ProxyTimeoutSeconds = Settings.DebugHttpTimeout.TotalSeconds;
            }

            DataConverter      = new JsonDataConverter();
            cadenceLogger      = LogManager.Default.GetLogger("cadence", isLogEnabledFunc: () => Settings.LogCadence);
            cadenceProxyLogger = LogManager.Default.GetLogger("cadence-proxy", isLogEnabledFunc: () => Settings.LogCadenceProxy);

            lock (syncLock)
            {
                try
                {
                    idToClient.Add(this.ClientId, this);

                    httpServer = new HttpServer(address, settings);
                    ListenUri  = httpServer.ListenUri;

                    // Determine the port we'll have [cadence-proxy] listen on and then
                    // fire up the cadence-proxy process.

                    proxyPort = !settings.DebugPrelaunched ? NetHelper.GetUnusedTcpPort(address) : debugProxyPort;

                    if (!Settings.DebugPrelaunched && proxyProcess == null)
                    {
                        proxyProcess = StartProxy(new IPEndPoint(address, proxyPort), settings, ClientId);
                    }
                }
                catch
                {
                    idToClient.Remove(this.ClientId);
                    throw;
                }
            }

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
                    // where the .NET Client is listening.

                    var initializeRequest =
                        new InitializeRequest()
                        {
                            LibraryAddress = ListenUri.Host,
                            LibraryPort    = ListenUri.Port,
                            LogLevel       = Settings.LogLevel
                        };

                    CallProxyAsync(initializeRequest).Wait();

                    // Send the [ConnectRequest] to the [cadence-proxy] telling it
                    // how to connect to the Cadence cluster.

                    var sbEndpoints = new StringBuilder();

                    foreach (var serverUri in settings.Servers)
                    {
                        var uri = new Uri(serverUri, UriKind.Absolute);

                        sbEndpoints.AppendWithSeparator($"{uri.Host}:{uri.Port}", ",");
                    }

                    var connectRequest = 
                        new ConnectRequest()
                        {
                            Endpoints     = sbEndpoints.ToString(),
                            Identity      = settings.ClientIdentity,
                            ClientTimeout = TimeSpan.FromSeconds(30),
                            Domain        = settings.DefaultDomain,
                            CreateDomain  = settings.CreateDomain
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
            // request timeouts.

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

            if (Settings != null && !Settings.DebugDisableHandshakes)
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
                        worker.Dispose();
                    }

                    // Signal the proxy to disconnect and then terminate.

                    CallProxyAsync(new DisconnectRequest()).Wait();

                    // Terminate the proxy if there are no remaining connections.

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

                        WorkflowBase.UnregisterClient(this);
                        ActivityBase.UnregisterClient(this);
                        proxyProcess.Kill();

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
        }

        /// <summary>
        /// Returns the locally unique ID for the client instance.
        /// </summary>
        internal long ClientId { get; private set; }

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
        /// was closed normally or due to an error by examining the <see cref="CadenceClientClosedArgs"/>
        /// arguments passed to the handler.
        /// </summary>
        public event CadenceClosedDelegate ConnectionClosed;

        /// <summary>
        /// Ensures that that client instance is not disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the client is disposed.</exception>
        internal void EnsureNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(CadenceClient));
            }
        }

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
        /// Returns the .NET type implementing the named Cadence activity.
        /// </summary>
        /// <param name="activityTypeName">The Cadence activity type name.</param>
        /// <returns>The workflow .NET type or <c>null</c> if the type was not found.</returns>
        internal Type GetActivityType(string activityTypeName)
        {
            Covenant.Requires<ArgumentNullException>(activityTypeName != null);

            lock (syncLock)
            {
                if (activityTypes.TryGetValue(activityTypeName, out var type))
                {
                    return type;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the Cadence task list to be referenced for an operation.  If <paramref name="taskList"/>
        /// is not <c>null</c> or empty then that will be returned otherwise <see cref="CadenceSettings.DefaultTaskList"/>
        /// will be returned.  Note that one of <paramref name="taskList"/> or the default task list
        /// must be non-empty.
        /// </summary>
        /// <param name="taskList">The specific task list to use or null/empty.</param>
        /// <returns>The task list to be referenced.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="taskList"/> and the default task list are both null or empty.</exception>
        internal string ResolveTaskList(string taskList)
        {
            EnsureNotDisposed();

            if (!string.IsNullOrEmpty(taskList))
            {
                return taskList;
            }
            else if (!string.IsNullOrEmpty(Settings.DefaultTaskList))
            {
                return Settings.DefaultTaskList;
            }

            throw new ArgumentNullException($"One of [{nameof(taskList)}] parameter or the client's default task list (specified as [{nameof(CadenceClient)}.{nameof(CadenceClient.Settings)}.{nameof(CadenceSettings.DefaultTaskList)}]) must be non-empty.");
        }

        /// <summary>
        /// Returns the Cadence domain to be referenced for an operation.  If <paramref name="domain"/>
        /// is not <c>null</c> or empty then that will be returned otherwise the  <see cref="CadenceSettings.DefaultDomain"/>
        /// will be returned.  Note that one of <paramref name="domain"/> or the default domain must
        /// be non-empty.
        /// </summary>
        /// <param name="domain">The specific domain to use or null/empty.</param>
        /// <returns>The domain to be referenced.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="domain"/> and the default domain are both null or empty.</exception>
        internal string ResolveDomain(string domain)
        {
            EnsureNotDisposed();

            if (!string.IsNullOrEmpty(domain))
            {
                return domain;
            }
            else if (!string.IsNullOrEmpty(Settings.DefaultDomain))
            {
                return Settings.DefaultDomain;
            }

            throw new ArgumentNullException($"One of [{nameof(domain)}] parameter or the client's default domain (specified as [{nameof(CadenceClient)}.{nameof(CadenceClient.Settings)}.{nameof(CadenceSettings.DefaultDomain)}]) must be non-empty.");
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
                        () =>
                        {
                            CallProxyAsync(new CancelRequest() { RequestId = requestId }).Wait();
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
        internal async Task ProxyReplyAsync(ProxyRequest request, ProxyReply reply)
        {
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentNullException>(reply != null);

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
                // happen) the HTTP request to the [cadence-proxy] failed
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
        /// for handling <b>cadence-proxy</b> request timeouts.
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

                            log.LogWarn(() => $" Request Timeout: [request={operation.Request.GetType().Name}, started={operation.StartTimeUtc.ToString(NeonHelper.DateFormat100NsTZ)}, timeout={operation.Timeout}].");

                            var notAwaitingThis = Task.Run(
                                async () =>
                                {
                                    if (operation.Request.Type != InternalMessageTypes.CancelRequest)
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
    }
}