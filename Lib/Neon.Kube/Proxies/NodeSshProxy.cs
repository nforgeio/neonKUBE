//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.cs
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

// This class spans of four source files.
//
//      NeonSshProxy.cs (this file)     - Common code
//      NodeSshProxy.BasePrepare.cs     - Configure base images
//      NodeSshProxy.ClusterSetup.cs    - Configure nodes string cluster setup
//      NodeSshProxy.NodePrepare.cs     - Configure node images

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
    public partial class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        private static readonly Regex idempotentRegex = new Regex(@"[a-z0-9\.-/]+", RegexOptions.IgnoreCase);

        private ClusterProxy    cluster;

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
            // Append the neonKUBE cluster binary folder to the remote path.

            RemotePath += $":{KubeNodeFolders.Bin}";
        }

        /// <summary>
        /// Returns the associated <see cref="ClusterProxy"/> when there is one.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when there is no associated cluster proxy.</exception>
        public ClusterProxy Cluster
        {
            get
            {
                if (this.cluster == null)
                {
                    throw new InvalidOperationException($"[{nameof(NodeSshProxy<TMetadata>)}] instance is not associated with a cluster proxy.");
                }

                return this.cluster;
            }

            set { this.cluster = value; }
        }

        /// <summary>
        /// Returns the associated <see cref="NodeDefinition"/> metadata when present.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when there is no associated node definition.</exception>
        public NodeDefinition NodeDefinition
        {
            get
            {
                var nodeDefinition = Metadata as NodeDefinition;

                if (nodeDefinition == null)
                {
                    throw new InvalidOperationException($"[{nameof(NodeSshProxy<TMetadata>)}] instance does not include a node definition.");
                }

                return nodeDefinition;
            }
        }

        /// <summary>
        /// Returns the NTP time sources to be used by the node.
        /// </summary>
        /// <returns>The quoted and space separated list of IP address or DNS hostnames for the node's NTP time sources in priority order.</returns>
        /// <remarks>
        /// <para>
        /// The cluster will be configured such that the first master node (by sorted name) will be the primary timesource
        /// for the cluster.  All other master and worker nodes will be configured to use the first master by default.
        /// Secondary masters will be configured to use the external timesource next so any master can automatically
        /// assume these duities.
        /// </para>
        /// <para>
        /// Worker nodes will be configured to use master node in sorted order but will not be configured to use the 
        /// external time sources to avoid having large clusters spam the sources.
        /// </para>
        /// <para>
        /// The nice thing about this is that the cluster will almost always be closly synchronized with the first master
        /// with gracefull fallback on node failures.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when there is no associated cluster proxy.</exception>
        public string GetNtpSources()
        {
            var clusterDefinition = Cluster.Definition;
            var nodeDefinition    = NodeDefinition;
            var sortedMasters     = clusterDefinition.SortedMasterNodes.ToArray();
            var firstMaster       = sortedMasters.First();
            var sbExternalSources = new StringBuilder();
            var sbNodeSources     = new StringBuilder();

            // Normalize the external time sources.

            foreach (var source in clusterDefinition.TimeSources)
            {
                sbExternalSources.AppendWithSeparator($"\"{source}\"");
            }

            if (sbExternalSources.Length == 0)
            {
                // Fallback to [pool.ntp.org] when no external source is specified.

                sbExternalSources.AppendWithSeparator("\"pool.ntp.org\"");
            }

            switch (nodeDefinition.Role)
            {
                case NodeRole.Master:

                    if (nodeDefinition.Name.Equals(firstMaster.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // The first master is configured to use the external time sources only.

                        sbNodeSources.AppendWithSeparator(sbExternalSources.ToString());
                    }
                    else
                    {
                        // The remaining masters are configured to prioritize the first master and
                        // then fallback to the external sources.

                        sbNodeSources.AppendWithSeparator($"\"{firstMaster.Address}\"");
                        sbNodeSources.AppendWithSeparator(sbExternalSources.ToString());
                    }
                    break;

                case NodeRole.Worker:

                    // Workers are configured to priortize the first master and then fall
                    // back to the remaining masters.  Workers will not be configured to
                    // use the external time sources to avoid having large clusters spam
                    // the sources.

                    foreach (var master in sortedMasters)
                    {
                        sbNodeSources.AppendWithSeparator($"\"{master.Address}\"");
                    }

                    sbNodeSources.AppendWithSeparator(sbExternalSources.ToString());
                    break;

                default:

                    throw new NotImplementedException();
            }

            return sbNodeSources.ToString();
        }

        /// <summary>
        /// Returns the connection information for SSH.NET.
        /// </summary>
        /// <returns>The connection information.</returns>
        private ConnectionInfo GetConnectionInfo()
        {
            var address = string.Empty;
            var port = SshPort;

            if (Cluster?.HostingManager != null)
            {
                var ep = Cluster.HostingManager.GetSshEndpoint(this.Name);

                address = ep.Address;
                port = ep.Port;
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

        /// <summary>
        /// Indicates whether an idempotent action has been completed.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        /// <returns><c>true</c> when the action has already been completed.</returns>
        public bool GetIdempotentState(string actionId)
        {
            return FileExists(LinuxPath.Combine(KubeNodeFolders.State, actionId));
        }

        /// <summary>
        /// Explicitly indicates that an idempotent action has been completed
        /// on the node.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        public void SetIdempotentState(string actionId)
        {
            SudoCommand($"mkdir -p {KubeNodeFolders.State} && touch {KubeNodeFolders.State}/{actionId}", RunOptions.FaultOnError);
        }

        /// <summary>
        /// Invokes a named action on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">The action to be performed.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="actionId"/> must uniquely identify the action on the node.
        /// This may include letters, digits, dashes and periods as well as one or
        /// more forward slashes that can be used to organize idempotent status files
        /// into folders.
        /// </para>
        /// <para>
        /// This method tracks successful action completion by creating a file
        /// on the node at <see cref="KubeNodeFolders.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public bool InvokeIdempotent(string actionId, Action action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId), nameof(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId), nameof(actionId));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            if (action.GetMethodInfo().ReturnType != typeof(void))
            {
                // Ensure that a "void" async method isn't being passed because that
                // would be treated as fire-and-forget and is not what developers
                // will expect.

                throw new ArgumentException($"Possible async delegate passed to [{nameof(InvokeIdempotent)}()]", nameof(action));
            }

            var stateFolder = KubeNodeFolders.State;
            var slashPos = actionId.LastIndexOf('/');

            if (slashPos != -1)
            {
                // Extract any folder path from the activity ID and add it to
                // the state folder path.

                stateFolder = LinuxPath.Combine(stateFolder, actionId.Substring(0, slashPos));
                actionId = actionId.Substring(slashPos + 1);

                Covenant.Assert(actionId.Length > 0);
            }

            var statePath = LinuxPath.Combine(stateFolder, actionId);

            SudoCommand($"mkdir -p {stateFolder}");

            if (FileExists(statePath))
            {
                return false;
            }

            action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}");
            }

            return true;
        }

        /// <summary>
        /// Invokes a named action asynchronously on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">The asynchronous action to be performed.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="actionId"/> must uniquely identify the action on the node.
        /// This may include letters, digits, dashes and periods as well as one or
        /// more forward slashes that can be used to organize idempotent status files
        /// into folders.
        /// </para>
        /// <para>
        /// This method tracks successful action completion by creating a file
        /// on the node at <see cref="KubeNodeFolders.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public async Task<bool> InvokeIdempotentAsync(string actionId, Func<Task> action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId), nameof(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId), nameof(actionId));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            var stateFolder = KubeNodeFolders.State;
            var slashPos = actionId.LastIndexOf('/');

            if (slashPos != -1)
            {
                // Extract any folder path from the activity ID and add it to
                // the state folder path.

                stateFolder = LinuxPath.Combine(stateFolder, actionId.Substring(0, slashPos));
                actionId = actionId.Substring(slashPos + 1);

                Covenant.Assert(actionId.Length > 0);
            }

            var statePath = LinuxPath.Combine(stateFolder, actionId);

            SudoCommand($"mkdir -p {stateFolder}");

            if (FileExists(statePath))
            {
                return false;
            }

            await action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}");
            }

            return true;
        }

        /// <summary>
        /// Ensures that the node operating system and version is supported for a neonKUBE
        /// cluster.  This faults the nodeproxy on faliure.
        /// </summary>
        /// <param name="statusWriter">Optional log writer action.</param>
        /// <returns><c>true</c> if the operation system is supported.</returns>
        public bool VerifyNodeOS(Action<string> statusWriter = null)
        {
            KubeHelper.WriteStatus(statusWriter, "Check", "Operating system");
            Status = "check: operating system";

            // $todo(jefflill): We're currently hardcoded to Ubuntu 20.04.x

            if (!OsName.Equals("Ubuntu", StringComparison.InvariantCultureIgnoreCase) || OsVersion < Version.Parse("20.04"))
            {
                Fault("Expected: Ubuntu 20.04+");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cleans a node by removing unnecessary package manager metadata, cached DHCP information, etc.
        /// and then fills unreferenced file system blocks and nodes with zeros so the disk image will
        /// compress better.
        /// </summary>
        /// <param name="statusWriter">Optional log writer action.</param>
        public void Clean(Action<string> statusWriter = null)
        {
            KubeHelper.WriteStatus(statusWriter, "Clean", "VM");
            Status = "clean: VM";

            var cleanScript =
@"#!/bin/bash
cloud-init clean
apt-get clean
rm -rf /var/lib/dhcp/*
fstrim /
";
            SudoCommand(CommandBundle.FromScript(cleanScript), RunOptions.FaultOnError);
        }
    }
}