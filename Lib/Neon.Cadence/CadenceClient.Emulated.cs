//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Emulated.cs
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

#if DEBUG

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
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Emulated [cadence-proxy] implementation:
        //
        // IMPLEMENTATION NOTE:
        // --------------------
        // This is a low-fidelity emulation of Cadence functionality.  This is intended
        // to be used for verifying some of the bigger picture .NET Cadence client functionality
        // independently from the [cadence-proxy].
        //
        // This is not intended for unit testing actual workflows.

        /// <summary>
        /// Used to track emulated Cadence domains.
        /// </summary>
        private class EmulatedCadenceDomain
        {
            public string       Name { get; set; }
            public string       Description { get; set; }
            public DomainStatus Status { get; set; }
            public string       OwnerEmail { get; set; }
            public string       Uuid { get; set; }
            public bool         EmitMetrics { get; set; }
            public int          RetentionDays { get; set; }
        }

        /// <summary>
        /// Used to track emulated Cadence worker (registrations).
        /// </summary>
        private class EmulatedWorker
        {
            public long WorkerId { get; set;}
        }

        /// <summary>
        /// Used to track emulated workflows.
        /// </summary>
        private class EmulatedWorkflow
        {
            /// <summary>
            /// The workflow ID.
            /// </summary>
            public string WorkflowId { get; set; }

            /// <summary>
            /// The workflow context ID.
            /// </summary>
            public long WorkflowContextId { get; set; }

            /// <summary>
            /// Identifies the Cadence domain hosting the workflow.
            /// </summary>
            public string Domain { get; set; }

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
            /// Indicates when the workflow as completed execution.
            /// </summary>
            public bool IsComplete { get; set; }

            /// <summary>
            /// The workflow result or <c>null</c>.
            /// </summary>
            public byte[] Result { get; set; }

            /// <summary>
            /// The workflow error or <c>null</c>.
            /// </summary>
            public CadenceError Error { get; set; }
        }

        private AsyncMutex                              emulationMutex                = new AsyncMutex();
        private List<EmulatedCadenceDomain>             emulatedDomains               = new List<EmulatedCadenceDomain>();
        private Dictionary<long, EmulatedWorker>        emulatedWorkers               = new Dictionary<long, EmulatedWorker>();
        private Dictionary<long, EmulatedWorkflow>      emulatedWorkflowContexts      = new Dictionary<long, EmulatedWorkflow>();
        private Dictionary<string, EmulatedWorkflow>    emulatedWorkflows             = new Dictionary<string, EmulatedWorkflow>();
        private Dictionary<long, Operation>             emulatedOperations            = new Dictionary<long, Operation>();
        private long                                    nextEmulatedWorkerId          = 0;
        private long                                    nextEmulatedWorkflowContextId = 0;
        private Thread                                  heartbeatThread;
        private Thread                                  timeoutThread;
        private IWebHost                                emulatedHost;

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

            await response.Body.WriteAsync(clonedMessage.Serialize());
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

            if (EmulatedLibraryClient == null && proxyMessage.Type != MessageTypes.InitializeRequest)
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

                case MessageTypes.CancelRequest:

                    await OnEmulatedCancelRequestAsync((CancelRequest)proxyMessage);
                    break;

                case MessageTypes.DomainDescribeRequest:

                    await OnEmulatedDomainDescribeRequestAsync((DomainDescribeRequest)proxyMessage);
                    break;

                case MessageTypes.DomainRegisterRequest:

                    await OnEmulatedDomainRegisterRequestAsync((DomainRegisterRequest)proxyMessage);
                    break;

                case MessageTypes.DomainUpdateRequest:

                    await OnEmulatedDomainUpdateRequestAsync((DomainUpdateRequest)proxyMessage);
                    break;

                case MessageTypes.HeartbeatRequest:

                    await OnEmulatedHeartbeatRequestAsync((HeartbeatRequest) proxyMessage);
                    break;

                case MessageTypes.InitializeRequest:

                    await OnEmulatedInitializeRequestAsync((InitializeRequest)proxyMessage);
                    break;

                case MessageTypes.ConnectRequest:

                    await OnEmulatedConnectRequestAsync((ConnectRequest)proxyMessage);
                    break;

                case MessageTypes.TerminateRequest:

                    await OnEmulatedTerminateRequestAsync((TerminateRequest)proxyMessage);
                    break;

                case MessageTypes.NewWorkerRequest:

                    await OnEmulatedNewWorkerRequestAsync((NewWorkerRequest)proxyMessage);
                    break;

                case MessageTypes.StopWorkerRequest:

                    await OnEmulatedStopWorkerRequestAsync((StopWorkerRequest)proxyMessage);
                    break;

                //-------------------------------------------------------------
                // Workflow messages

                case MessageTypes.WorkflowExecuteRequest:

                    await OnEmulatedWorkflowExecuteRequestAsync((WorkflowExecuteRequest)proxyMessage);
                    break;

                case MessageTypes.WorkflowRegisterRequest:

                    await OnEmulatedWorkflowRegisterRequestAsync((WorkflowRegisterRequest)proxyMessage);
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
            var worker   = new EmulatedWorker() { WorkerId = workerId };

            using (await emulationMutex.AcquireAsync())
            {
                if (!emulatedDomains.Any(d => d.Name == request.Domain))
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
                domain = emulatedDomains.SingleOrDefault(d => d.Name == request.Name);
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
                if (emulatedDomains.SingleOrDefault(d => d.Name == request.Name) != null)
                {
                    reply.Error = new CadenceDomainAlreadyExistsException($"Domain [{request.Name}] already exists.").ToCadenceError();

                    await EmulatedLibraryClient.SendReplyAsync(request, reply);
                    return;
                }

                emulatedDomains.Add(
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
                domain = emulatedDomains.SingleOrDefault(d => d.Name == request.Name);
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
            var workflowContextId = Interlocked.Increment(ref nextEmulatedWorkflowContextId);

            var workflow = new EmulatedWorkflow()
            {
                WorkflowId        = request.Options.ID ?? Guid.NewGuid().ToString("D"),
                WorkflowContextId = workflowContextId,
                Args              = request.Args,
                Domain            = request.Domain,
                Name              = request.Name,
                Options           = request.Options
            };

            using (await emulationMutex.AcquireAsync())
            {
                emulatedWorkflowContexts.Add(workflowContextId, workflow);
                emulatedWorkflows.Add(workflow.WorkflowId, workflow);
            }

            await EmulatedLibraryClient.SendReplyAsync(request,
                new WorkflowExecuteReply()
                {
                    Execution = new InternalWorkflowExecution()
                    {
                        ID    = workflow.WorkflowId,
                        RunID = workflow.WorkflowId
                    }
                });

            // If we wanted to get fancy here, we'd have a queue with pending workflow
            // executions and have a separate thread pick these up and invoke them
            // only when a worker is registered for the domain and task list.
            //
            // But in keeping with our limited emulation goals, we're just going to
            // kick off a task here to invoke the workflow.

            _ = Task.Run(
                async () =>
                {
                    var workflowInvokeRequest =
                        new WorkflowInvokeRequest()
                        {
                            Args              = workflow.Args,
                            Name              = workflow.Name,
                            WorkflowContextId = workflow.WorkflowContextId
                        };

                    var workflowInvokeReply = (WorkflowInvokeReply)await CallClientAsync(workflowInvokeRequest);

                    using (await emulationMutex.AcquireAsync())
                    {
                        workflow.Result     = workflowInvokeReply.Result;
                        workflow.Error      = workflowInvokeReply.Error;
                        workflow.IsComplete = true;
                    }
                });
        }
    }
}

#endif
