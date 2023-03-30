//-----------------------------------------------------------------------------
// FILE:	    PortForwardManager.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Buffers.Binary;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Net;

using k8s;
using System.Diagnostics.Contracts;
using Neon.Retry;

namespace Neon.Kube.PortForward
{
    /// <inheritdoc/>
    public class PortForwardManager : IPortForwardManager
    {
        private readonly IKubernetes                k8s;
        private readonly IPortForwardStreamManager  streamManager;
        private readonly ILoggerFactory             loggerFactory;
        private readonly ILogger                    logger;

        private ConcurrentDictionary<string, Task> containerPortForwards;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">Specifies the Kubernetes client.</param>
        /// <param name="loggerFactory">Optionally specifies the a logger factory.</param>
        public PortForwardManager(
            IKubernetes     k8s,
            ILoggerFactory  loggerFactory = null)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s                   = k8s;
            this.loggerFactory         = loggerFactory;
            this.logger                = loggerFactory?.CreateLogger<PortForwardManager>();
            this.streamManager         = new PortForwardStreamManager(loggerFactory);
            this.containerPortForwards = new ConcurrentDictionary<string, Task>();
        }

        /// <inheritdoc/>
        public void StartPodPortForward(
            string                           name,
            string                           @namespace,
            int                              localPort,
            int                              remotePort,
            IPAddress                        localAddress      = null,
            Dictionary<string, List<string>> customHeaders     = null,
            CancellationToken                cancellationToken = default)
        {
            Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(localPort), nameof(localPort), $"Invalid TCP port: {localPort}");
            Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(remotePort), nameof(remotePort), $"Invalid TCP port: {remotePort}");

            var key = $"{@namespace}/{name}";

            if (containerPortForwards.ContainsKey(key))
            {
                return;
            }

            if (localAddress == null)
            {
                localAddress = IPAddress.Loopback;
            }

            var forwardingTask = Task.Run(
                async () =>
                {
                    using (var portListener = new PortListener(localPort, localAddress, loggerFactory, cancellationToken))
                    {
                        try
                        {
                            logger?.LogDebugEx(() => $"Starting listener for forwarding: {localPort} --> {remotePort}");

                            RemoteConnectionFactory remoteConnectionFactory = 
                                async () =>
                                {
                                    logger?.LogDebugEx(() => $"Creating socket forwarding: {localPort} --> {remotePort}");

                                    var retry = new LinearRetryPolicy(typeof(WebSocketException), maxAttempts: 3, retryInterval: TimeSpan.FromMilliseconds(100));

                                    return await retry.InvokeAsync(
                                        async () =>
                                        {
                                            return await k8s.WebSocketNamespacedPodPortForwardAsync(
                                                name:                 name,
                                                @namespace:           @namespace,
                                                ports:                new int[] { remotePort },
                                                webSocketSubProtocol: WebSocketProtocol.V4BinaryWebsocketProtocol,
                                                customHeaders:        customHeaders);
                                        });
                                };

                            while (!cancellationToken.IsCancellationRequested)
                            {
                                var localConnection = await portListener.Listener.AcceptTcpClientAsync();

                                streamManager.Start(
                                    localConnection:         localConnection,
                                    remoteConnectionFactory: remoteConnectionFactory,
                                    remotePort:              remotePort,
                                    cancellationToken:       cancellationToken);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            logger.LogDebugEx(() => $"Port forwarding was canceled");
                        }
                        catch (Exception e)
                        {
                            logger?.LogErrorEx(e, () => $"Port forwarding {@namespace}/{name} {localPort}:{remotePort} failed.");
                            throw;
                        }
                    }
                }, 
                cancellationToken);

            cancellationToken.Register(
                () => 
                {
                    containerPortForwards.Remove(key, out _);
                });

            containerPortForwards.TryAdd(key, forwardingTask);
        }
    }
}
