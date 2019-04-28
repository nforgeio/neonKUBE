//-----------------------------------------------------------------------------
// FILE:	    CadenceConnection.Emulated.cs
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
    public partial class CadenceConnection
    {

        //---------------------------------------------------------------------
        // Emulated [cadence-proxy] implementation:

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
        /// Handles requests to the emulated <b>cadence-proxy</b> root <b>"/"</b> endpoint path.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedRootRequestAsync(HttpContext context)
        {
            var request        = context.Request;
            var response       = context.Response;
            var requestMessage = ProxyMessage.Deserialize<ProxyMessage>(request.Body);

            if (EmulatedLibraryClient == null && requestMessage.Type != MessageTypes.InitializeRequest)
            {
                response.StatusCode = StatusCodes.Status400BadRequest;
                await response.WriteAsync($"Unexpected Message: Waiting for an [{nameof(InitializeRequest)}] message to specify the [cadence-client] network endpoint.");
                return;
            }

            switch (requestMessage.Type)
            {
                case MessageTypes.InitializeRequest:

                    await OnEmulatedInitializeRequestAsync((InitializeRequest)requestMessage);
                    break;

                case MessageTypes.HeartbeatRequest:

                    await OnEmulatedHeartbeatRequestAsync((HeartbeatRequest) requestMessage);
                    break;

                case MessageTypes.CancelRequest:

                    await OnEmulatedCancelRequestAsync((CancelRequest)requestMessage);
                    break;

                default:

                    response.StatusCode = StatusCodes.Status400BadRequest;
                    await response.WriteAsync($"EMULATION: Message [{requestMessage.Type}] is not supported.");
                    break;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles emulated <see cref="InitializeRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedInitializeRequestAsync(InitializeRequest request)
        {
            lock (syncLock)
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
        /// Handles emulated <see cref="HeartbeatRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedHeartbeatRequestAsync(HeartbeatRequest request)
        {
            await EmulatedLibraryClient.SendReplyAsync(request, new HeartbeatReply());
        }

        /// <summary>
        /// Handles emulated <see cref="CancelRequest"/> messages.
        /// </summary>
        /// <param name="request">The received message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task OnEmulatedCancelRequestAsync(CancelRequest request)
        {
            var cancelReply = new CancelReply()
            {
                WasCancelled = false
            };

            lock (syncLock)
            {
                if (operations.TryGetValue(request.TargetRequestId, out var operation))
                {
                    operations.Remove(request.TargetRequestId);
                    cancelReply.WasCancelled = true;
                }
            }

            await EmulatedLibraryClient.SendReplyAsync(request, cancelReply);
        }

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
    }
}
