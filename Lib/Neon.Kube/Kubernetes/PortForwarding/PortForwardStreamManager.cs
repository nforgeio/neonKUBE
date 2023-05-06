//-----------------------------------------------------------------------------
// FILE:	    PortForwardStreamManager.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Net;

namespace Neon.Kube.PortForward
{
    /// <inheritdoc/>
    internal class PortForwardStreamManager : IPortForwardStreamManager
    {
        private readonly ILoggerFactory     loggerFactory;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loggerFactory">Optionally specifies a logger factory.</param>
        public PortForwardStreamManager(ILoggerFactory loggerFactory = null)
        {
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc/>
        public void Start(
            TcpClient               localConnection,
            RemoteConnectionFactory remoteConnectionFactory, 
            int                     remotePort, 
            CancellationToken       cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(localConnection != null, nameof(localConnection));
            Covenant.Requires<ArgumentNullException>(remoteConnectionFactory != null, nameof(remoteConnectionFactory));
            Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(remotePort), () => nameof(remotePort), () => $"Invalid TCP port: {remotePort}");

            var stream = new PortForwardStream(localConnection, remoteConnectionFactory, remotePort, loggerFactory);

            stream.RunAsync(cancellationToken);
        }
    }
}
