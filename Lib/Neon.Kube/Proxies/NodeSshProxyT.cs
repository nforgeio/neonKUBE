//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxyT.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using ICSharpCode.SharpZipLib.Zip;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

// $todo(jefflill):
//
// The download methods don't seem to be working for paths like [/proc/meminfo].
// They return an empty stream.

// $todo(jefflill):
//
// Most of this code has been copied to the [Neon.SSH.NET] project under
// the [Neon.SSH] namespace.  There's just a tiny bit of extra functionality
// implemented by this and the derived [LinuxSshProxy] class.
//
// We should convert this class to inherit from the [Neon.SSH.NET] class
// so we don't have to maintain duplicate code.
//
//      https://github.com/nforgeio/neonKUBE/issues/1006

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Uses an SSH/SCP connection to provide access to Linux machines to access
    /// files, run commands, etc.
    /// </para>
    /// <note>
    /// This is class is <b>not intended</b> to be a <b>general purpose SSH wrapper</b> 
    /// at this time.  It currently assumes that the remote side is running some variant
    /// of Linux and it makes some global changes including disabling SUDO password prompts
    /// for all users as well as creating some global directories.
    /// </note>
    /// </summary>
    /// <typeparam name="TMetadata">
    /// Defines the metadata type the application wishes to associate with the server.
    /// You may specify <c>object</c> when no additional metadata is required.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Construct an instance to connect to a specific cluster node.  You may specify
    /// <typeparamref name="TMetadata"/> to associate application specific information
    /// or state with the instance.
    /// </para>
    /// <para>
    /// This class includes methods to invoke Linux commands on the node,
    /// </para>
    /// <para>
    /// Call <see cref="LinuxSshProxy{TMetadata}.Dispose()"/> or <see cref="LinuxSshProxy{TMetadata}.Disconnect()"/>
    /// to close the connection.
    /// </para>
    /// <note>
    /// You can use <see cref="Clone()"/> to make a copy of a proxy that can be
    /// used to perform parallel operations against the same machine.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        /// <summary>
        /// Constructs a <see cref="LinuxSshProxy{TMetadata}"/>.
        /// </summary>
        /// <param name="name">The display name for the server.</param>
        /// <param name="address">The private cluster IP address for the server.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="port">Optionally overrides the standard SSH port (22).</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public NodeSshProxy(string name, IPAddress address, SshCredentials credentials, int port = NetworkPorts.SSH, TextWriter logWriter = null)
            : base(name, address, credentials, port, logWriter)
        {
        }

        /// <summary>
        /// The associated <see cref="ClusterProxy"/> or <c>null</c>.
        /// </summary>
        public ClusterProxy Cluster { get; internal set; }

        /// <summary>
        /// Returns the connection information for SSH.NET.
        /// </summary>
        /// <returns>The connection information.</returns>
        private ConnectionInfo GetConnectionInfo()
        {
            var address = string.Empty;
            var port    = SshPort;

            if (Cluster?.HostingManager != null)
            {
                var ep = Cluster.HostingManager.GetSshEndpoint(this.Name);

                address = ep.Address;
                port    = ep.Port;
            }
            else
            {
                address = Address.ToString();
            }

            var connectionInfo = new ConnectionInfo(address, port, credentials.Username, GetAuthenticationMethod(credentials))
            {
                Timeout = ConnectTimeout
            };

            // Ensure that we use a known good encryption mechanism.

            var encryptionName = "aes256-ctr";

            foreach (var disabledEncryption in connectionInfo.Encryptions
                .Where(e => e.Key != encryptionName)
                .ToList())
            {
                connectionInfo.Encryptions.Remove(disabledEncryption.Key);
            }

            return connectionInfo;
        }

        /// <summary>
        /// Returns a clone of the SSH proxy.  This can be useful for situations where you
        /// need to be able to perform multiple SSH/SCP operations against the same
        /// machine in parallel.
        /// </summary>
        /// <returns>The cloned <see cref="NodeSshProxy{TMetadata}"/>.</returns>
        public new NodeSshProxy<TMetadata> Clone()
        {
            var clone = new NodeSshProxy<TMetadata>(Name, Address, credentials, SshPort, logWriter);

            LinuxSshProxy<TMetadata>.CloneTo(this, clone);

            return clone;
        }
    }
}
