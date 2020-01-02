//-----------------------------------------------------------------------------
// FILE:	    DockerClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Net.Http.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Retry;
using Neon.Tasks;

namespace Neon.Docker
{
    /// <summary>
    /// Implements a client that can submit commands to a Docker engine via the Docker Remote API.
    /// </summary>
    public partial class DockerClient : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a <see cref="JObject"/> value into an instance of the specified
        /// type by converting it to JSON and then parsing that.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="value">The input dynamic.</param>
        /// <returns>The parsed value.</returns>
        public static T ParseObject<T>(JObject value)
            where T : class, new()
        {
            if (value == null)
            {
                return null;
            }

            var json = value.ToString();

            return NeonHelper.JsonDeserialize<T>(json);
        }

        //---------------------------------------------------------------------
        // Instance members

        private HttpMessageHandler  handler;
        private string              baseUri;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The settings</param>
        public DockerClient(DockerSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null, nameof(settings));

            // Select a custom managed handler when Docker is listening on a Unix
            // domain socket, otherwise use the standard handler.

            if (settings.Uri.Scheme.Equals("unix", StringComparison.OrdinalIgnoreCase))
            {
                baseUri = "http://unix.sock";
                handler = new ManagedHandler(
                    async (string host, int port, CancellationToken cancellationToken) =>
                    {
                        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                        // $todo(jefflill):
                        //
                        // It looks like .NET Core 3.0 (and presumably .NET Standard 2.1 in the near future
                        // implements this as [System.Net.Sockets.UnixDomainSocketEndPoint].  Look into
                        // converting to this standard class in the future and dumping my hacked implementation.

                        await sock.ConnectAsync(new Microsoft.Net.Http.Client.UnixDomainSocketEndPoint(settings.Uri.LocalPath));

                        return sock;
                    });
            }
            else
            {
                baseUri = settings.Uri.ToString();
                handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
                };
            }

            // Ensure that the [baseUri] doesn't end with a "/".

            if (baseUri.EndsWith("/"))
            {
                baseUri = baseUri.Substring(0, baseUri.Length - 1);
            }

            this.Settings   = settings;
            this.JsonClient = new JsonClient(handler, disposeHandler: true)
            {
                SafeRetryPolicy = settings.RetryPolicy
            };
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                JsonClient.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the version of the Docker Remote API implemented by this class.
        /// </summary>
        public Version ApiVersion { get; private set; } = new Version("1.23");

        /// <summary>
        /// Returns the <see cref="DockerSettings"/>.
        /// </summary>
        public DockerSettings Settings { get; private set; }

        /// <summary>
        /// Returns the underlying <see cref="JsonClient"/>.
        /// </summary>
        public JsonClient JsonClient { get; private set; }

        /// <summary>
        /// Returns the URI for a specific command.
        /// </summary>
        /// <param name="command">The command name.</param>
        /// <param name="item">The optionak sub item.</param>
        /// <returns>The command URI.</returns>
        private string GetUri(string command, string item = null)
        {
            if (string.IsNullOrEmpty(item))
            {
                return $"{baseUri}/{command}";
            }
            else
            {
                return $"{baseUri}/{command}/{item}";
            }
        }

        /// <summary>
        /// Ping the remote Docker engine to verify that it's ready.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> if ready.</returns>
        /// <remarks>
        /// <note>
        /// This method does not use a <see cref="IRetryPolicy"/>.
        /// </note>
        /// </remarks>
        public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.ClearAsync;

            try
            {
                var httpResponse = await JsonClient.HttpClient.GetAsync(GetUri("_ping"), cancellationToken);

                return httpResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Waits for the Docker engine or Swarm manager to be ready to accept 
        /// requests.
        /// </summary>
        /// <param name="timeout">The maximum timne to wait (defaults to 120 seconds).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <para>
        /// The Swarm Manager can return unexpected HTTP response codes when it is
        /// not ready to accept requests.  For example, a request to <b>/volumes</b>
        /// may return a <b>404: Not Found</b> response rather than the <b>503: Service Unavailable</b>
        /// that one would expect.   The server can return this even when <see cref="PingAsync"/>
        /// return successfully.
        /// </para>
        /// <para>
        /// This method attempts to ensure that the server is really ready.
        /// </para>
        /// </remarks>
        public async Task WaitUntilReadyAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentException>(timeout == null || timeout >= TimeSpan.Zero, nameof(timeout));

            // Create a transient detector that extends [TransientDetector.Network] to
            // consider HTTP 404 (not found) as transient too.

            Func<Exception, bool> transientDetector =
                e =>
                {
                    if (TransientDetector.NetworkOrHttp(e))
                    {
                        return true;
                    }

                    var httpException = e as HttpException;

                    if (httpException != null)
                    {
                        return httpException.StatusCode == HttpStatusCode.NotFound;
                    }

                    return false;
                };

            timeout = timeout ?? TimeSpan.FromSeconds(120);

            IRetryPolicy retryPolicy;

            if (timeout == TimeSpan.Zero)
            {
                retryPolicy = NoRetryPolicy.Instance;
            }
            else
            {
                // We're going to use a [LinearRetryPolicy] that pings the server every
                // two seconds for the duration of the requested timeout period.

                var retryInterval = TimeSpan.FromSeconds(2);

                retryPolicy = new LinearRetryPolicy(transientDetector, maxAttempts: (int)(timeout.Value.TotalSeconds / retryInterval.TotalSeconds), retryInterval: retryInterval);
            }

            await JsonClient.GetAsync(retryPolicy, GetUri("info"), cancellationToken: cancellationToken);

            // $hack(jefflill):
            //
            // At this point, the server should be ready but I'm still seeing 500 errors
            // when listing Docker volumes.  I'm going to add an additional request to
            // list the volumes and not return until at least one volume is present.

            await retryPolicy.InvokeAsync(
                async () =>
                {
                    var volumesResponse = new VolumeListResponse(await JsonClient.GetAsync(NoRetryPolicy.Instance, GetUri("volumes"), cancellationToken: cancellationToken));

                    if (volumesResponse.Volumes.Count == 0)
                    {
                        throw new TransientException("Docker node reports no volumes.");
                    }
                });
        }
    }
}
