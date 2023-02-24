//-----------------------------------------------------------------------------
// FILE:	    IPortListener.cs
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
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Neon.Kube.PortForward
{
    /// <summary>
    /// Describe port listeners used by <see cref="IPortForwardManager"/> implementations
    /// to listen for connections on the local workstation that will be forwarded to a
    /// pod running in the cluster.
    /// </summary>
    internal interface IPortListener : IDisposable
    {
        /// <summary>
        /// Returns the associated <see cref="TcpListener"/>.
        /// </summary>
        TcpListener Listener { get; }
    }
}
