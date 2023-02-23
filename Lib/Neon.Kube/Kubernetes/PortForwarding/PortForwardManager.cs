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

using k8s;

namespace Neon.Kube
{
    /// <inheritdoc/>
    public class PortForwardManager : IPortForwardManager
    {
        private readonly IKubernetes    k8s;
        private readonly IStreamManager streamManager;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger        logger;

        private ConcurrentDictionary<string, Task> containerPortForwards;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s"></param>
        /// <param name="loggerFactory"></param>
        public PortForwardManager(
            IKubernetes k8s,
            ILoggerFactory loggerFactory = null)
        {
            this.k8s                   = k8s;
            this.loggerFactory         = loggerFactory;
            this.logger                = loggerFactory?.CreateLogger<PortForwardManager>();
            this.streamManager         = new StreamManager(loggerFactory);
            this.containerPortForwards = new ConcurrentDictionary<string, Task>();
        }

        /// <summary>
        /// <see cref="IPortForwardManager.StartPodPortForward"/>
        /// </summary>
        public void StartPodPortForward(
            string                           name,
            string                           @namespace,
            int                              localPort,
            int                              remotePort,
            Dictionary<string, List<string>> customHeaders     = null,
            CancellationToken                cancellationToken = default(CancellationToken))
        {
            var key = $"{@namespace}/{name}";
            if (containerPortForwards.ContainsKey(key))
            {
                return;
            }

            var task = Task.Run(async () =>
            {
                using (var portListener = new PortListener(localPort, loggerFactory, cancellationToken))
                {
                    try
                    {
                        logger?.LogDebugEx(() => $"Starting listener for forwarding {localPort}:{remotePort}");

                        Func<Task<WebSocket>> createWebSocketAsync = async () =>
                        {
                            logger?.LogDebugEx(() => $"Creating socket forwarding {localPort}:{remotePort}");

                            int tries = 0;
                            Exception exception = null;
                            while (tries < 3)
                            {
                                try
                                {
                                    return await this.k8s.WebSocketNamespacedPodPortForwardAsync(
                                        name:                 name,
                                        @namespace:           @namespace,
                                        ports:                new int[] { remotePort },
                                        webSocketSubProtocol: WebSocketProtocol.V4BinaryWebsocketProtocol,
                                        customHeaders:        customHeaders);
                                }
                                catch (WebSocketException e)
                                {
                                    exception = e;
                                    await Task.Delay(100);
                                }
                            }
                            throw exception;
                        };

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var localConnection = await portListener.Listener.AcceptTcpClientAsync();

                            streamManager.Start(
                                localConnection:         localConnection,
                                remoteConnectionFactory: createWebSocketAsync,
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
                        logger?.LogErrorEx(() => $"Port forwarding {@namespace}/{name} {localPort}:{remotePort} failed with exception : {e.Message}");
                        throw;
                    }
                }
            }, cancellationToken);

            cancellationToken.Register(
                () => 
                {
                    containerPortForwards.Remove(key, out _);
                });

            containerPortForwards.TryAdd(key, task);
        }
    }
}
