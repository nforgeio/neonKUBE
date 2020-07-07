//-----------------------------------------------------------------------------
// FILE:	    DesktopService.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Net.Http.Server;

using Neon.Common;
using Neon.Kube;
using Neon.Net;

// IMPLEMENTATION NOTE:
// --------------------
// I'm using [Microsoft.Net.Http.Server.WebListener] directly to implement this 
// rather than cranking up the entire ASP.NET stack, to hopefully reduce the
// overhead.

namespace WinDesktop
{
    /// <summary>
    /// Exposes a very simple HTTP API service on <see cref="KubeClientConfig.DesktopServicePort"/>
    /// that is queried by the <b>neon-cli</b> via the <see cref="DesktopClient"/>.
    /// </summary>
    /// <remarks>
    /// This server uses a primitive authortization mechanism that should be fine
    /// since the server will typically be listening on the loopback interface.
    /// The client must set the <b>Authorization</b> header to the <see cref="KubeClientConfig.InstallationId"/>
    /// as a bearer token.
    /// </remarks>
    public static class DesktopService
    {
        private static object       syncLock = new object();
        private static WebListener  listener;

        /// <summary>
        /// Starts the service if it's not already running.
        /// </summary>
        public static void Start()
        {
            lock (syncLock)
            {
                if (listener != null)
                {
                    return; // Already running.
                }

                try
                {
                    var settings = new WebListenerSettings();

                    settings.UrlPrefixes.Add($"http://localhost:{KubeHelper.ClientConfig.DesktopServicePort}/");

                    listener = new WebListener(settings);
                    listener.Start();

                    // Handle received requests in a background task.

                    Task.Run(() => RequestProcessor());
                }
                catch
                {
                    listener = null;
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the service if it's not already stopped.
        /// </summary>
        public static void Stop()
        {
            lock (syncLock)
            {
                if (listener == null)
                {
                    return; // Already stopped.
                }

                try
                {
                    listener.Dispose();
                }
                finally
                {
                    listener = null;
                }
            }
        }

        /// <summary>
        /// Handles received requests.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task RequestProcessor()
        {
            while (true)
            {
                try
                {
                    var newContext = await listener.AcceptAsync();

                    var task = Task.Factory.StartNew(
                        async (object arg) =>
                        {
                            using (var context = (RequestContext)arg)
                            {
                                var request  = context.Request;
                                var response = context.Response;

                                // Primitive authentication/authorization implementation:

                                if (!request.Headers.TryGetValue("Authorization", out var values) || 
                                    !values.First().Equals($"Bearer {KubeHelper.ClientConfig.InstallationId}", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    response.StatusCode   = 403;
                                    response.ReasonPhrase = "Access Denied";

                                    return;
                                }

                                // We only support a handful of methods and endpoints.

                                switch (request.Method.ToUpperInvariant())
                                {
                                    case "POST":

                                        switch (request.Path)
                                        {
                                            case "/update-ui":

                                                await OnUpdateUIAsync(request, response);
                                                break;

                                            case "/start-operation":

                                                await OnRemoteStartOperation(request, response);
                                                break;

                                            case "/end-operation":

                                                await OnRemoteEndOperation(request, response);
                                                break;

                                            case "/login":

                                                await OnLogin(request, response);
                                                break;

                                            case "/logout":

                                                await OnLogout(request, response);
                                                break;
                                        }
                                        break;

                                    default:

                                        response.StatusCode   = 405;
                                        response.ReasonPhrase = "Method Not Allowed";
                                        break;
                                }
                            }
                        },
                        newContext);
                }
                catch (ObjectDisposedException)
                {
                    return; // We're going to use this as the signal to stop.
                }
            }
        }

        /// <summary>
        /// Parse the request body as a JSON document.
        /// </summary>
        /// <typeparam name="TResult">The document type.</typeparam>
        /// <param name="request">The request.</param>
        /// <returns>The parsed body.</returns>
        private static async Task<TResult> ParseBodyAsync<TResult>(Request request)
        {
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[16 * 1024];

                while (true)
                {
                    var cb = await request.Body.ReadAsync(buffer, 0, buffer.Length);

                    if (cb == 0)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, cb);
                }

                return NeonHelper.JsonDeserialize<TResult>(Encoding.UTF8.GetString(ms.ToArray()));
            }            
        }

        /// <summary>
        /// Called to update the UI state.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response.</param>
        /// <returns>Th tracking <see cref="Task"/>.</returns>
        private static async Task OnUpdateUIAsync(Request request, Response response)
        {
            MainForm.Current.PostUpdateUIState();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called to signal the start of a long-running remote operation.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response.</param>
        /// <returns>Th tracking <see cref="Task"/>.</returns>
        private static async Task OnRemoteStartOperation(Request request, Response response)
        {
            MainForm.Current.OnRemoteStartOperation(await ParseBodyAsync<RemoteOperation>(request));
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called to signal the end of a long-running remote operation.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response.</param>
        /// <returns>Th tracking <see cref="Task"/>.</returns>
        private static async Task OnRemoteEndOperation(Request request, Response response)
        {
            MainForm.Current.OnEndOperation(await ParseBodyAsync<RemoteOperation>(request));
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called to signal that the workstation has logged into a new cluster.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response.</param>
        /// <returns>Th tracking <see cref="Task"/>.</returns>
        private static async Task OnLogin(Request request, Response response)
        {
            MainForm.Current.OnLogin();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called to signal that the workstation has logged out of a cluster.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response.</param>
        /// <returns>Th tracking <see cref="Task"/>.</returns>
        private static async Task OnLogout(Request request, Response response)
        {
            MainForm.Current.OnLogout();
            await Task.CompletedTask;
        }
    }
}
