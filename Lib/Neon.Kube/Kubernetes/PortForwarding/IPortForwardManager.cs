//-----------------------------------------------------------------------------
// FILE:        IPortForwardManager.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using System.Net;

namespace Neon.Kube.PortForward
{
    /// <summary>
    /// Manages port-forwarding from the local workstation to remote pods running in the cluster.
    /// </summary>
    public interface IPortForwardManager
    {
        /// <summary>
        /// Establishes a <see cref="PortForwardStream"/> connection from the local
        /// workstation toa pod running in the cluster.
        /// </summary>
        /// <param name="namespaceName">Specifies the remote pod namespace.</param>
        /// <param name="podName">Specifies the remote pod name.</param>
        /// <param name="localPort">Specifies the local port on the workstation.</param>
        /// <param name="remotePort">Specifies the target port for the remotye pod.</param>
        /// <param name="localAddress">Specifies the listen port. If not set, <see cref="IPAddress.Loopback"/> is used.</param>
        /// <param name="customHeaders">Optionally specifies any custom connection headers.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        void StartPodPortForward(
            string                           namespaceName,
            string                           podName,
            int                              localPort,
            int                              remotePort,
            IPAddress                        localAddress      = null,
            Dictionary<string, List<string>> customHeaders     = null,
            CancellationToken                cancellationToken = default);
    }
}
