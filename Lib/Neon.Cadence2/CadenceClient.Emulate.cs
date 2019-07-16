//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Emulate.cs
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

using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

// $todo(jeff.lill):
//
// Emulation is only very partially implemented right now.  It would be nice to
// have a high-fidelity in-memory implementation but this isn't a super high
// priority.  Here's the tracking issue:
//
//      https://github.com/nforgeio/neonKUBE/issues/556

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Emulated [cadence-proxy] implementation:

        /// <summary>
        /// Used to track emulated Cadence domains.
        /// </summary>
        private class EmulatedCadenceDomain
        {
            public string           Name { get; set; }
            public string           Description { get; set; }
            public DomainStatus     Status { get; set; }
            public string           OwnerEmail { get; set; }
            public string           Uuid { get; set; }
            public bool             EmitMetrics { get; set; }
            public int              RetentionDays { get; set; }
        }

        /// <summary>
        /// Used to track emulated qworkflow queries.
        /// </summary>
        private struct EmulatedQuery
        {
            public EmulatedQuery(string queryName, byte[] args)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName));

                this.QueryName = queryName;
                this.QueryArgs = args;
            }

            public string QueryName { get; private set; }
            public byte[] QueryArgs { get; private set; }
        }

        /// <summary>
        /// Used to track emulated workflow signals.
        /// </summary>
        private struct EmulatedSignal
        {
            public EmulatedSignal(string queryName, byte[] args)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName));

                this.SignalName = queryName;
                this.SignalArgs = args;
            }

            public string SignalName { get; private set; }
            public byte[] SignalArgs { get; private set; }
        }

        /// <summary>
        /// Used to track an emulated workflow.
        /// </summary>
        private class EmulatedWorkflow
        {
            /// <summary>
            /// The workflow ID.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// The workflow RunID.
            /// </summary>
            public string RunId { get; set; }

            /// <summary>
            /// The workflow context ID.
            /// </summary>
            public long ContextId { get; set; }

            /// <summary>
            /// Identifies the Cadence domain hosting the workflow.
            /// </summary>
            public string Domain { get; set; }

            /// <summary>
            /// Identifies the task list hosting the workflow.
            /// </summary>
            public string TaskList { get; set; }

            /// <summary>
            /// Identifies the workflow implementation to be started.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The workflow arguments encoded a a byte array (or <c>null</c>).
            /// </summary>
            public byte[] Args { get; set; }

            /// <summary>
            /// The workflow start options.
            /// </summary>
            public InternalStartWorkflowOptions Options { get; set; }

            /// <summary>
            /// Identifies global vs. child workflows.
            /// </summary>
            public bool IsGlobal { get; set; }

            /// <summary>
            /// Indicates when the workflow as completed execution.
            /// </summary>
            public bool IsComplete { get; set; }

            /// <summary>
            /// Indicates that the workflow has been canceled.
            /// </summary>
            public bool IsCanceled { get; set; }

            /// <summary>
            /// The workflow result or <c>null</c>.
            /// </summary>
            public byte[] Result { get; set; }

            /// <summary>
            /// The workflow error or <c>null</c>.
            /// </summary>
            public CadenceError Error { get; set; }

            /// <summary>
            /// Set when the workflow is executing on a worker.
            /// </summary>
            public Worker Worker { get; set; }

            /// <summary>
            /// Raised when the workflow has completed.
            /// </summary>
            public AsyncManualResetEvent CompletedEvent { get; private set; } = new AsyncManualResetEvent();

            /// <summary>
            /// The list of executing child workflows.
            /// </summary>
            public List<EmulatedWorkflow> ChildWorkflows { get; private set; } = new List<EmulatedWorkflow>();

            /// <summary>
            /// The list of executing child activities.
            /// </summary>
            public List<EmulatedActivity> ChildActivities { get; private set; } = new List<EmulatedActivity>();

            /// <summary>
            /// Pending queries targeting the workflow.
            /// </summary>
            public Queue<EmulatedQuery> PendingQueries { get; private set; } = new Queue<EmulatedQuery>();

            /// <summary>
            /// Pending signals targeting the workflow.
            /// </summary>
            public Queue<EmulatedSignal> PendingSignals { get; private set; } = new Queue<EmulatedSignal>();
        }

        /// <summary>
        /// Used to track an emulated workflow.
        /// </summary>
        private class EmulatedActivity
        {
            /// <summary>
            /// The activity ID.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// The activity context ID.
            /// </summary>
            public long ContextId { get; set; }

            /// <summary>
            /// Set when the activity is executing on a worker.
            /// </summary>
            public Worker Worker { get; set; }
        }

        /// <summary>
        /// Used to track emulated Cadence worker (registrations).
        /// </summary>
        private class EmulatedWorker
        {
            /// <summary>
            /// The worker ID.
            /// </summary>
            public long WorkerId { get; set; }

            /// <summary>
            /// Indicates whether the worker handles workflows or activities.
            /// </summary>
            public bool IsWorkflow { get; set; }

            /// <summary>
            /// The worker domain.
            /// </summary>
            public string Domain { get; set; }

            /// <summary>
            /// The worker task list.
            /// </summary>
            public string TaskList { get; set; }

            /// <summary>
            /// The workflows currently executing on the worker.
            /// </summary>
            public List<EmulatedWorkflow> Workflows { get; private set; } = new List<EmulatedWorkflow>();

            /// <summary>
            /// The activities currently executing on the worker.
            /// </summary>
            public List<EmulatedActivity> Activities { get; private set; } = new List<EmulatedActivity>();
        }

        private AsyncMutex                                  emulationMutex           = new AsyncMutex();
        private Dictionary<string, EmulatedCadenceDomain>   emulatedDomains          = new Dictionary<string, EmulatedCadenceDomain>();
        private Dictionary<long, EmulatedWorker>            emulatedWorkers          = new Dictionary<long, EmulatedWorker>();
        private Dictionary<long, EmulatedWorkflow>          emulatedWorkflowContexts = new Dictionary<long, EmulatedWorkflow>();
        private Dictionary<long, EmulatedActivity>          emulatedActivityContexts = new Dictionary<long, EmulatedActivity>();
        private Dictionary<string, EmulatedWorkflow>        emulatedWorkflows        = new Dictionary<string, EmulatedWorkflow>();
        private Dictionary<long, Operation>                 emulatedOperations       = new Dictionary<long, Operation>();
        private List<EmulatedWorkflow>                      emulatedPendingWorkflows = new List<EmulatedWorkflow>();
        private long                                        nextEmulatedWorkerId     = 0;
        private long                                        nextEmulatedContextId    = 0;
        private IWebHost                                    emulatedHost;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Set this to <c>false</c> to emulate an unhealthy
        /// <b>cadence-proxy</b>.
        /// </summary>
        internal bool EmulatedHealth { get; set; } = true;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Configured as the HTTP client the emulated 
        /// [cadence-proxy] implementation uses to communicate with the [cadence-client]
        /// after the first <see cref="InitializeRequest"/> has been received.
        /// </summary>
        internal HttpClient EmulatedLibraryClient { get; private set; }

        /// <summary>
        /// Called when an HTTP request is received by the integrated web server 
        /// (presumably from the the associated <b>cadence-proxy</b> process).
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedHttpRequestAsync(HttpContext context)
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

                        await OnEmulatedRootRequestAsync(context);
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

            var stream = clonedMessage.SerializeAsStream();

            try
            {
                await stream.CopyToAsync(response.Body);
            }
            finally
            {
                MemoryStreamPool.Free(stream);
            }
        }

        /// <summary>
        /// Handles requests to the emulated <b>cadence-proxy</b> root <b>"/"</b> endpoint path.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedRootRequestAsync(HttpContext context)
        {
            var request      = context.Request;
            var response     = context.Response;
            var proxyMessage = ProxyMessage.Deserialize<ProxyMessage>(request.Body);

            if (EmulatedLibraryClient == null && proxyMessage.Type != InternalMessageTypes.InitializeRequest)
            {
                response.StatusCode = StatusCodes.Status400BadRequest;
                await response.WriteAsync($"Unexpected Message: Waiting for an [{nameof(InitializeRequest)}] message to specify the [cadence-client] network endpoint.");
                return;
            }

            // Handle proxy reply messages by completing any pending [CallClientAsync()] operation.

            var reply = proxyMessage as ProxyReply;

            if (reply != null)
            {
                Operation operation;

                using (await emulationMutex.AcquireAsync())
                {
                    emulatedOperations.TryGetValue(reply.RequestId, out operation);    
                }

                if (operation != null)
                {
                    if (reply.Type != operation.Request.ReplyType)
                    {
                        response.StatusCode = StatusCodes.Status400BadRequest;
                        await response.WriteAsync($"[cadence-emulation] has a request [type={operation.Request.Type}, requestId={operation.RequestId}] pending but reply [type={reply.Type}] is not valid and will be ignored.");
                    }
                    else
                    {
                        operation.SetReply(reply);
                        response.StatusCode = StatusCodes.Status200OK;
                    }
                }
                else
                {
                    log.LogWarn(() => $"[cadence-emulation] reply [type={reply.Type}, requestId={reply.RequestId}] does not map to a pending operation and will be ignored.");

                    response.StatusCode = StatusCodes.Status400BadRequest;
                    await response.WriteAsync($"[cadence-emulation] does not have a pending operation with [requestId={reply.RequestId}].");
                }

                return;
            }

            // Handle proxy request messages.

            switch (proxyMessage.Type)
            {
                //-------------------------------------------------------------
                // Client messages

                case InternalMessageTypes.CancelRequest:

                    await OnEmulatedCancelRequestAsync((CancelRequest)proxyMessage);
                    break;

                case InternalMessageTypes.DomainDescribeRequest:

                    await OnEmulatedDomainDescribeRequestAsync((DomainDescribeRequest)proxyMessage);
                    break;

                case InternalMessageTypes.DomainRegisterRequest:

                    await OnEmulatedDomainRegisterRequestAsync((DomainRegisterRequest)proxyMessage);
                    break;

                case InternalMessageTypes.DomainUpdateRequest:

                    await OnEmulatedDomainUpdateRequestAsync((DomainUpdateRequest)proxyMessage);
                    break;

                case InternalMessageTypes.HeartbeatRequest:

                    await OnEmulatedHeartbeatRequestAsync((HeartbeatRequest) proxyMessage);
                    break;

                case InternalMessageTypes.InitializeRequest:

                    await OnEmulatedInitializeRequestAsync((InitializeRequest)proxyMessage);
                    break;

                case InternalMessageTypes.ConnectRequest:

                    await OnEmulatedConnectRequestAsync((ConnectRequest)proxyMessage);
                    break;

                case InternalMessageTypes.TerminateRequest:

                    await OnEmulatedTerminateRequestAsync((TerminateRequest)proxyMessage);
                    break;

                case InternalMessageTypes.NewWorkerRequest:

                    await OnEmulatedNewWorkerRequestAsync((NewWorkerRequest)proxyMessage);
                    break;

                case InternalMessageTypes.StopWorkerRequest:

                    await OnEmulatedStopWorkerRequestAsync((StopWorkerRequest)proxyMessage);
                    break;

                case InternalMessageTypes.PingRequest:

                    await OnEmulatedPingRequestAsync((PingRequest)proxyMessage);
                    break;

                //-------------------------------------------------------------
                // Workflow messages

                case InternalMessageTypes.WorkflowExecuteRequest:

                    await OnEmulatedWorkflowExecuteRequestAsync((WorkflowExecuteRequest)proxyMessage);
                    break;

                case InternalMessageTypes.WorkflowRegisterRequest:

                    await OnEmulatedWorkflowRegisterRequestAsync((WorkflowRegisterRequest)proxyMessage);
                    break;

                case InternalMessageTypes.WorkflowSetCacheSizeRequest:

                    await OnEmulatedWorkflowSetCacheSizeRequestAsync((WorkflowSetCacheSizeRequest)proxyMessage);
                    break;

                case InternalMessageTypes.WorkflowGetResultRequest:

                    await OnEmulatedWorkflowGetResultRequestAsync((WorkflowGetResultRequest)proxyMessage);
                    break;

                //-------------------------------------------------------------

                default:

                    response.StatusCode = StatusCodes.Status400BadRequest;
                    await response.WriteAsync($"EMULATION: Message [{proxyMessage.Type}] is not supported.");
                    break;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Asynchronously emulates a call to the <b>cadence-client</b> by sending a request message
        /// and then waits for a reply.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private async Task<ProxyReply> CallClientAsync(ProxyRequest request)
        {
            try
            {
                var requestId = Interlocked.Increment(ref nextRequestId);
                var operation = new Operation(requestId, request);

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

        //---------------------------------------------------------------------
        // Global messages

        /// <summary>
        /// Handles emulated <see cref="InitializeRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedInitializeRequestAsync(InitializeRequest request)
        {
            using (await emulationMutex.AcquireAsync())
            {
                if (EmulatedLibraryClient == null)
                {
                    var httpHandler = new HttpClientHandler()
                    {
                        // Disable compression because all communication is happening on
                        // a loopback interface (essentially in-memory) so there's not
                        // much point in taking the CPU hit to do compression.

                        AutomaticDecompression = DecompressionMethods.None
                    };

                    EmulatedLibraryClient = new HttpClient(httpHandler, disposeHandler: true)
                    {
                        BaseAddress = new Uri($"http://{request.LibraryAddress}:{request.LibraryPort}")
                    };
                }
            }

            await EmulatedLibraryClient.SendReplyAsync(request, new InitializeReply());
        }

        /// <summary>
        /// Handles emulated <see cref="ConnectRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedConnectRequestAsync(ConnectRequest request)
        {
            await EmulatedLibraryClient.SendReplyAsync(request, new ConnectReply());
        }

        /// <summary>
        /// Handles emulated <see cref="HeartbeatRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedHeartbeatRequestAsync(HeartbeatRequest request)
        {
            if (Settings.DebugIgnoreHeartbeats)
            {
                // Ignore heartbeats so unit tests can verify the correct behavior.

                return;
            }

            await EmulatedLibraryClient.SendReplyAsync(request, new HeartbeatReply());
        }

        /// <summary>
        /// Handles emulated <see cref="CancelRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedCancelRequestAsync(CancelRequest request)
        {
            var reply = new CancelReply()
            {
                WasCancelled = false
            };

            using (await emulationMutex.AcquireAsync())
            {
                if (operations.TryGetValue(request.TargetRequestId, out var operation))
                {
                    operations.Remove(request.TargetRequestId);
                    reply.WasCancelled = true;
                }
            }

            await EmulatedLibraryClient.SendReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles emulated <see cref="TerminateRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedTerminateRequestAsync(TerminateRequest request)
        {
            await EmulatedLibraryClient.SendReplyAsync(request, new TerminateReply());
        }

        /// <summary>
        /// Handles emulated <see cref="NewWorkerRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedNewWorkerRequestAsync(NewWorkerRequest request)
        {
            var workerId = Interlocked.Increment(ref nextEmulatedWorkerId);
            var worker   = new EmulatedWorker()
            {
                WorkerId = workerId,
                Domain   = request.Domain,
                TaskList = request.TaskList
            };

            using (await emulationMutex.AcquireAsync())
            {
                if (!emulatedDomains.ContainsKey(request.Domain))
                {
                    await EmulatedLibraryClient.SendReplyAsync(request,
                        new NewWorkerReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Domain [{request.Domain}] does not exist.").ToCadenceError()
                        });

                    return;
                }

                // We need to track the worker so we can avoid sending
                // stop requests to the [cadence-proxy] when the worker
                //is already stopped.

                emulatedWorkers.Add(workerId, worker);
            }

            await EmulatedLibraryClient.SendReplyAsync(request, new NewWorkerReply() { WorkerId = workerId });
        }

        /// <summary>
        /// Handles emulated <see cref="DomainDescribeRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedDomainDescribeRequestAsync(DomainDescribeRequest request)
        {
            var reply  = new DomainDescribeReply();
            var domain = (EmulatedCadenceDomain)null;

            if (string.IsNullOrEmpty(request.Name))
            {
                reply.Error = new CadenceEntityNotExistsException("Invalid name.").ToCadenceError();

                await EmulatedLibraryClient.SendReplyAsync(request, reply);
                return;
            }

            using (await emulationMutex.AcquireAsync())
            {
                if (!emulatedDomains.TryGetValue(request.Name, out domain))
                {
                    domain = null;
                }
            }

            if (domain == null)
            {
                reply.Error = new CadenceEntityNotExistsException($"Domain [name={request.Name}] does not exist.").ToCadenceError();
            }
            else
            {
                reply.DomainInfoName             = domain.Name;
                reply.DomainInfoOwnerEmail       = domain.OwnerEmail;
                reply.DomainInfoStatus           = domain.Status;
                reply.DomainInfoDescription      = domain.Description;
                reply.ConfigurationEmitMetrics   = domain.EmitMetrics;
                reply.ConfigurationRetentionDays = domain.RetentionDays;
            }

            await EmulatedLibraryClient.SendReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles emulated <see cref="DomainRegisterRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedDomainRegisterRequestAsync(DomainRegisterRequest request)
        {
            var reply = new DomainRegisterReply();

            if (string.IsNullOrEmpty(request.Name))
            {
                await EmulatedLibraryClient.SendReplyAsync(request,
                    new DomainRegisterReply()
                    {
                        Error = new CadenceBadRequestException("Invalid name.").ToCadenceError()
                    });

                await EmulatedLibraryClient.SendReplyAsync(request, new DomainRegisterReply());
                return;
            }

            using (await emulationMutex.AcquireAsync())
            {
                if (emulatedDomains.ContainsKey(request.Name))
                {
                    reply.Error = new CadenceDomainAlreadyExistsException($"Domain [{request.Name}] already exists.").ToCadenceError();

                    await EmulatedLibraryClient.SendReplyAsync(request, reply);
                    return;
                }

                emulatedDomains.Add(
                    request.Name,
                    new EmulatedCadenceDomain()
                    {
                        Name          = request.Name,
                        Description   = request.Description,
                        OwnerEmail    = request.OwnerEmail,
                        Status        = DomainStatus.Registered,
                        Uuid          = Guid.NewGuid().ToString("D"),
                        EmitMetrics   = request.EmitMetrics,
                        RetentionDays = request.RetentionDays
                    });

                await EmulatedLibraryClient.SendReplyAsync(request, reply);
            }
        }

        /// <summary>
        /// Handles emulated <see cref="DomainUpdateRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedDomainUpdateRequestAsync(DomainUpdateRequest request)
        {
            var reply = new DomainUpdateReply();

            if (string.IsNullOrEmpty(request.Name))
            {
                reply.Error = new CadenceBadRequestException("Domain name is required.").ToCadenceError();

                await EmulatedLibraryClient.SendReplyAsync(request, reply);
                return;
            }

            EmulatedCadenceDomain domain;

            using (await emulationMutex.AcquireAsync())
            {
                if (!emulatedDomains.TryGetValue(request.Name, out domain))
                {
                    domain = null;
                }
            }

            if (domain == null)
            {
                reply.Error = new CadenceEntityNotExistsException($"Domain [name={request.Name}] does not exist.").ToCadenceError();

                await EmulatedLibraryClient.SendReplyAsync(request, reply);
                return;
            }

            domain.Description   = request.UpdatedInfoDescription;
            domain.OwnerEmail    = request.UpdatedInfoOwnerEmail;
            domain.EmitMetrics   = request.ConfigurationEmitMetrics;
            domain.RetentionDays = request.ConfigurationRetentionDays;

            await EmulatedLibraryClient.SendReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles emulated <see cref="StopWorkerRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedStopWorkerRequestAsync(StopWorkerRequest request)
        {
            using (await emulationMutex.AcquireAsync())
            {
                if (emulatedWorkers.TryGetValue(request.WorkerId, out var worker))
                {
                    emulatedWorkers.Remove(request.WorkerId);

                    await EmulatedLibraryClient.SendReplyAsync(request, new StopWorkerReply());
                }
                else
                {
                    await EmulatedLibraryClient.SendReplyAsync(request,
                        new StopWorkerReply()
                        {
                            Error = new CadenceError() { String = "EntityNotExistsError", Type = "custom" }
                        });
                }
            }
        }

        /// <summary>
        /// Handles emulated <see cref="PingRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedPingRequestAsync(PingRequest request)
        {
            await EmulatedLibraryClient.SendReplyAsync(request, new PingReply());
        }

        //---------------------------------------------------------------------
        // Workflow messages

        /// <summary>
        /// Handles emulated <see cref="WorkflowRegisterRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedWorkflowRegisterRequestAsync(WorkflowRegisterRequest request)
        {
            await EmulatedLibraryClient.SendReplyAsync(request, new WorkflowRegisterReply());
        }

        /// <summary>
        /// Handles emulated <see cref="WorkflowExecuteRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedWorkflowExecuteRequestAsync(WorkflowExecuteRequest request)
        {
            var contextId = Interlocked.Increment(ref nextEmulatedContextId);

            var workflow = new EmulatedWorkflow()
            {
                Id        = request.Options?.ID ?? Guid.NewGuid().ToString("D"),
                RunId     = Guid.NewGuid().ToString("D"),
                ContextId = contextId,
                Args      = request.Args,
                // Domain    = request.Domain,  // $todo(jeff.lill): Will need to get this from the new workflow client context
                TaskList  = request.Options?.TaskList ?? CadenceClient.DefaultTaskList,
                Name      = request.Workflow,
                Options   = request.Options,
                IsGlobal  = true
            };

            using (await emulationMutex.AcquireAsync())
            {
                // Add the workflow to the list of pending workflows so that
                // the emulation thread can pick them up.

                emulatedPendingWorkflows.Add(workflow);
            }

            await EmulatedLibraryClient.SendReplyAsync(request,
                new WorkflowExecuteReply()
                {
                    Execution = new InternalWorkflowExecution()
                    {
                        ID    = workflow.Id,
                        RunID = workflow.RunId
                    }
                });
        }

        /// <summary>
        /// Handles emulated <see cref="WorkflowSetCacheSizeRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedWorkflowSetCacheSizeRequestAsync(WorkflowSetCacheSizeRequest request)
        {
            await EmulatedLibraryClient.SendReplyAsync(request, new WorkflowSetCacheSizeReply());
        }

        /// <summary>
        /// Handles emulated <see cref="WorkflowGetResultRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedWorkflowGetResultRequestAsync(WorkflowGetResultRequest request)
        {
            var workflow = await GetWorkflowAsync(request.RunId);

            if (workflow == null)
            {
                await EmulatedLibraryClient.SendReplyAsync(request,
                    new WorkflowGetResultReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [RunID={request.RunId}] does not exist.").ToCadenceError()
                    });

                return;
            }

            await workflow.CompletedEvent.WaitAsync();

            await EmulatedLibraryClient.SendReplyAsync(request,
                new WorkflowGetResultReply()
                {
                    Error  = workflow.Error,
                    Result = workflow.Result
                });
        }

        //---------------------------------------------------------------------
        // Emulation implementation:

        /// <summary>
        /// Fetches an executing or pending workflow by it's RunID.
        /// </summary>
        /// <param name="runId">The workflow RunID.</param>
        /// <returns>The workflow or <c>null</c>.</returns>
        private async Task<EmulatedWorkflow> GetWorkflowAsync(string runId)
        {
            using (await emulationMutex.AcquireAsync())
            {
                var workflow = emulatedWorkflows.Values.SingleOrDefault(wf => wf.RunId == runId);

                if (workflow != null)
                {
                    return workflow;
                }

                return emulatedPendingWorkflows.SingleOrDefault(wf => wf.RunId == runId);
            }
        }

        /// <summary>
        /// Searches an emulated workflow worker for specified domain and task list.
        /// </summary>
        /// <param name="domain">The domain.</param>
        /// <param name="taskList">The task list.</param>
        /// <returns>The <see cref="EmulatedWorker"/> or <c>null</c>.</returns>
        private EmulatedWorker GetWorkflowWorker(string domain, string taskList)
        {
            return emulatedWorkers.Values.SingleOrDefault(worker => worker.IsWorkflow && worker.Domain == domain && worker.TaskList == taskList);
        }

        /// <summary>
        /// Handles emulation related background activities.
        /// </summary>
        private async Task EmulationTaskAsync()
        {
            var pollDelay = TimeSpan.FromMilliseconds(250);

            try
            {
                while (!closingConnection)
                {
                    using (await emulationMutex.AcquireAsync())
                    {
                        await Task.Delay(pollDelay);
                        await ExecutePendingWorkflowsAsync();
                    }
                }

                // Cancel all running workflows and activities.

                await CancelRunningWorkflowsAsync();
            }
            catch (Exception e)
            {
                // We shouldn't see any exceptions here except perhaps
                // [TaskCanceledException] when the connection is in
                // the process of being closed.

                if (!closingConnection || !(e is TaskCanceledException))
                {
                    log.LogError(e);
                }
            }
        }

        /// <summary>
        /// Executes any pending workflows that also have a matching worker running.
        /// </summary>
        private async Task ExecutePendingWorkflowsAsync()
        {
            var startedWorkflows = new List<EmulatedWorkflow>();

            foreach (var workflow in emulatedPendingWorkflows)
            {
                var worker = GetWorkflowWorker(workflow.Domain, workflow.TaskList);

                if (worker != null)
                {
                    startedWorkflows.Add(workflow);

                    _ = Task.Run(
                        async () =>
                        {
                            var workflowInvokeRequest =
                                new WorkflowInvokeRequest()
                                {
                                    Args      = workflow.Args,
                                    Name      = workflow.Name,
                                    ContextId = workflow.ContextId
                                };

                            var workflowInvokeReply = (WorkflowInvokeReply)await CallClientAsync(workflowInvokeRequest);

                            workflow.Result     = workflowInvokeReply.Result;
                            workflow.Error      = workflowInvokeReply.Error;
                            workflow.IsComplete = true;

                            workflow.CompletedEvent.Set();
                        });
                }
            }

            foreach (var workflow in startedWorkflows)
            {
                emulatedWorkflows.Add(workflow.RunId, workflow);
                emulatedPendingWorkflows.Remove(workflow);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Cancels all running workflows.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CancelRunningWorkflowsAsync()
        {
            // Just go ahead and clear these.

            emulatedPendingWorkflows.Clear();

            // Cancel all running external workflows, recursively canceling and child workflows
            // and activities.

            foreach (var workflow in emulatedWorkflows.Values.Where(wf => wf.IsGlobal && !wf.IsComplete))
            {
                await CallClientAsync(
                    new WorkflowTerminateRequest()
                    {
                        RunId      = workflow.RunId,
                        WorkflowId = workflow.Id
                    });

                workflow.IsCanceled = true;
                workflow.IsComplete = true;

                workflow.CompletedEvent.Set();
            }
        }

        /// <summary>
        /// Cancels a workflow along with and decendant workflows and activities.
        /// </summary>
        /// <param name="workflow">The workflow being canceled.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CancelWorkflowAsync(EmulatedWorkflow workflow)
        {
            foreach (var childWorkflow in workflow.ChildWorkflows)
            {
                await CancelWorkflowAsync(childWorkflow);
            }

            workflow.ChildWorkflows.Clear();

            foreach (var childActivity in workflow.ChildActivities)
            {
                await CancelActivityAsync(childActivity);
            }

            workflow.ChildActivities.Clear();
        }

        /// <summary>
        /// Cancels an activity.
        /// </summary>
        /// <param name="activity">The activity being canceled.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CancelActivityAsync(EmulatedActivity activity)
        {
            await CallClientAsync(
                new ActivityStoppingRequest()
                {
                    ContextId  = activity.ContextId,
                    ActivityId = activity.Id
                });

            emulatedActivityContexts.Remove(activity.ContextId);
        }
    }
}
