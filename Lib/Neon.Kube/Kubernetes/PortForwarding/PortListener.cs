//-----------------------------------------------------------------------------
// FILE:        PortListener.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Microsoft.Extensions.Logging;

using Neon.Net;

namespace Neon.Kube.PortForward
{
    /// <inheritdoc/>
    internal sealed class PortListener : IPortListener
    {
        private bool                            disposed = false;
        private readonly ILogger<PortListener>  logger;
        private int                             localPort;
        private IPAddress                       localAddress;
        private CancellationTokenRegistration   ctr;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="localPort">Specifies the local port.</param>
        /// <param name="localAddress">The local IP Address. Defaults to <see cref="IPAddress.Loopback"/>.</param>
        /// <param name="loggerFactory">Optionally specifies the logger factory.</param>
        /// <param name="cancellationToken">Optionally specifies the cancellation token.</param>
        public PortListener(
            int               localPort, 
            IPAddress         localAddress      = null,
            ILoggerFactory    loggerFactory     = null,
            CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(localPort), () => nameof(localPort), () => $"Invalid TCP port: {localPort}");

            this.localPort    = localPort;
            this.localAddress = localAddress ?? IPAddress.Loopback;
            this.logger       = loggerFactory?.CreateLogger<PortListener>();

            Listener = new TcpListener(localAddress, localPort);
            logger?.LogDebug($"PortListener created on: {localAddress}:{localPort}");

            ctr = cancellationToken.Register(() => this.Dispose());

            Listener.Start(512);
            logger?.LogDebug($"PortListener started on: {localAddress}:{localPort}");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            logger?.LogDebug($"PortListener stopped on: {localAddress}:{localPort}");
            Listener.Stop();
            ctr.Dispose();
        }

        /// <inheritdoc/>
        public TcpListener Listener { get; private set; }
    }
}
