//-----------------------------------------------------------------------------
// FILE:        IPortForwardStreamManager.cs
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
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Kube.PortForward
{
    /// <summary>
    /// Describes a manager that handles <see cref="PortForwardStream"/> lifecycle.
    /// </summary>
    internal interface IPortForwardStreamManager
    {
        /// <summary>
        /// Establishes a port forward stream connection from the local workstation to 
        /// a pod running in the cluster.
        /// </summary>
        /// <param name="localConnection">Specifies the local workstation side of the connection.</param>
        /// <param name="remoteConnectionFactory">Specifies the factory to be used to establish the remote side of the connection.</param>
        /// <param name="remotePort">Specifies the remote port.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        void Start(
            TcpClient               localConnection, 
            RemoteConnectionFactory remoteConnectionFactory, 
            int                     remotePort, 
            CancellationToken       cancellationToken = default);
    }
}
