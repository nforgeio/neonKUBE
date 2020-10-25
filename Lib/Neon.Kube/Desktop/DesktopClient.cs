//-----------------------------------------------------------------------------
// FILE:	    DesktopClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;
using Neon.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Implements an HTTP client that will be used by the <b>neon-cli</b>
    /// tool for communicating with the neonDESKTOP application running
    /// on the same machine.
    /// </para>
    /// <note>
    /// Calls to the desktop application will fail silently if the desktop
    /// doesn't respond because the desktop may not be running and these
    /// are really just nice status notifications, not anything critical.
    /// </note>
    /// </summary>
    public sealed class DesktopClient : IDisposable
    {
        private JsonClient client;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="serviceUri">Base URI for the desktop API service.</param>
        /// <param name="timeout">Optional request timeout (defaults to <b>500ms</b>).</param>
        internal DesktopClient(string serviceUri, TimeSpan timeout = default)
        {
            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromMilliseconds(500);
            }

            client = new JsonClient()
            {
                BaseAddress = new Uri(serviceUri),
                Timeout     = timeout
            };

            // We're going to pass the client installation ID as a simple
            // authentication mechanism.

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", KubeHelper.ClientConfig.InstallationId);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }

        /// <summary>
        /// Signals the desktop application to update its UI state.  This
        /// will generally be called after <b>neon-cli</b> has modified
        /// the cluster connection status.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will fail silently if the desktop application does
        /// not respond.
        /// </note>
        /// </remarks>
        public async Task UpdateUIAsync()
        {
            await SyncContext.ClearAsync;

            try
            {
                await client.PostAsync("update-ui", true);
            }
            catch
            {
                // Intentionally ignoring this.
            }
        }

        /// <summary>
        /// Signals to the Desktop application that the workstation has logged
        /// into a cluster.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will fail silently if the desktop application does
        /// not respond.
        /// </note>
        /// </remarks>
        public async Task Login()
        {
            await SyncContext.ClearAsync;

            try
            {
                await client.PostAsync(NoRetryPolicy.Instance, "login", true);
            }
            catch
            {
                // Intentionally ignoring this.
            }
        }

        /// <summary>
        /// Signals to the Desktop application that the workstation has logged
        /// out of a cluster.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will fail silently if the desktop application does
        /// not respond.
        /// </note>
        /// </remarks>
        public async Task Logout()
        {
            await SyncContext.ClearAsync;

            try
            {
                await client.PostAsync(NoRetryPolicy.Instance, "logout", true);
            }
            catch
            {
                // Intentionally ignoring this.
            }
        }

        /// <summary>
        /// Signals the desktop application that a long-running operation such
        /// as cluster setup is starting.
        /// </summary>
        /// <param name="summary">A brief summary of the operation.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will fail silently if the desktop application does
        /// not respond.
        /// </note>
        /// </remarks>
        public async Task StartOperationAsync(string summary)
        {
            await SyncContext.ClearAsync;

            var operation = new RemoteOperation()
            {
                Summary   = summary,
                ProcessId = Process.GetCurrentProcess().Id
            };

            try
            {
                await client.PostAsync(NoRetryPolicy.Instance, "start-operation", operation);
            }
            catch
            {
                // Intentionally ignoring this.
            }
        }

        /// <summary>
        /// Signals the desktop application the a long-running operation has
        /// completed.
        /// </summary>
        /// <param name="completedToast">
        /// Optionally specifies text to be displayed as toast by the 
        /// desktop application.
        /// </param>
        /// <param name="failed">Optionally indicates that the operation failed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will fail silently if the desktop application does
        /// not respond.
        /// </note>
        /// </remarks>
        public async Task EndOperationAsync(string completedToast = null, bool failed = false)
        {
            await SyncContext.ClearAsync;

            var operation = new RemoteOperation()
            {
                ProcessId      = Process.GetCurrentProcess().Id,
                CompletedToast = completedToast,
                Failed         = failed
            };

            try
            {
                await client.PostAsync(NoRetryPolicy.Instance, "end-operation", operation);
            }
            catch
            {
                // Intentionally ignoring this.
            }
        }
    }
}
