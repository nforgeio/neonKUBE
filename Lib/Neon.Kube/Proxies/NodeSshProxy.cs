//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Uses a SSH/SCP connection to provide access to Linux machines to access
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
    /// Call <see cref="LinuxSshProxy.Dispose()"/> or <see cref="LinuxSshProxy.Disconnect()"/>
    /// to close the connection.
    /// </para>
    /// <note>
    /// You can use <see cref="Clone()"/> to make a copy of a proxy that can be
    /// used to perform parallel operations against the same machine.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public partial class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>, INodeSshProxy
        where TMetadata : class
    {
        private static readonly Regex   idempotentRegex  = new Regex(@"[a-z0-9\.-/]+", RegexOptions.IgnoreCase);

        private ClusterProxy        cluster;
        private StringBuilder       internalLogBuilder;
        private TextWriter          internalLogWriter;

        /// <summary>
        /// Constructs a <see cref="LinuxSshProxy{TMetadata}"/>.
        /// </summary>
        /// <param name="name">The display name for the server.</param>
        /// <param name="address">The private cluster IP address for the server.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="role">Optionally specifies one of the <see cref="NodeRole"/> values identifying what the node does.</param>
        /// <param name="port">Optionally overrides the standard SSH port (22).</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public NodeSshProxy(string name, IPAddress address, SshCredentials credentials, string role = null, int port = NetworkPorts.SSH, TextWriter logWriter = null)
            : base(name, address, credentials, port, logWriter)
        {
            this.Role = role;

            // Append the neonKUBE cluster binary folder to the remote path.

            RemotePath += $":{KubeNodeFolder.Bin}";

            // We're going to maintain an internal log writer as well as the external writer
            // so that we'll always have easy access to the log even when the external writer
            // isn't open.

            internalLogBuilder = new StringBuilder();
            internalLogWriter  = new StringWriter(internalLogBuilder);
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

            set => this.cluster = value;
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

        /// <inheritdoc/>
        public string Role { get; set; }

        /// <summary>
        /// Indicates the type of node image type.  This is stored in the <b>/etc/neonkube/image-type</b> file.
        /// </summary>
        public KubeImageType ImageType
        {
            get
            {
                if (FileExists(KubeConst.ImageTypePath))
                {
                    return NeonHelper.ParseEnum<KubeImageType>(DownloadText(KubeConst.ImageTypePath).Trim(), KubeImageType.Unknown);
                }
                else
                {
                    return KubeImageType.Unknown;
                }
            }

            set
            {
                UploadText(KubeConst.ImageTypePath, NeonHelper.EnumToString(value), permissions: "664", owner: "sysadmin");
            }
        }

        /// <summary>
        /// <para>
        /// Indicates the neonKUBE node image version.  This is stored in the <b>/etc/neonkube/image-version</b> file.
        /// This can be used to ensure that the node image is compatible with the code configuring the cluster.
        /// </para>
        /// <node>
        /// This returns <c>null</c> when the <b>/etc/neonkube/image-version</b> file doesn't exist.
        /// </node>
        /// </summary>
        /// <exception cref="FormatException">Thrown when the version file could not be parsed.</exception>
        public SemanticVersion ImageVersion
        {
            get
            {
                if (!FileExists(KubeConst.ImageVersionPath))
                {
                    return null;
                }

                return SemanticVersion.Parse(base.DownloadText(KubeConst.ImageVersionPath));
            }

            set
            {
                if (value == null)
                {
                    if (FileExists(KubeConst.ImageVersionPath))
                    {
                        RemoveFile(KubeConst.ImageVersionPath);
                    }
                }
                else
                {
                    UploadText(KubeConst.ImageVersionPath, value.ToString());
                }
            }
        }

        /// <summary>
        /// Indicates that the node is a pre-built neon-desktop cluster.  This uses
        /// the existence of the <b>/etc/neonkube/prebuilt-desktop</b> file on the
        /// node to indicate this condition.
        /// </summary>
        public bool IsPrebuiltCluster
        {
            get => FileExists(KubeConst.ImagePrebuiltDesktopPath);

            set
            {
                if (value)
                {
                    if (FileExists(KubeConst.ImagePrebuiltDesktopPath))
                    {
                        RemoveFile(KubeConst.ImagePrebuiltDesktopPath);
                    }
                }
                else
                {
                    SudoCommand("touch", KubeConst.ImagePrebuiltDesktopPath).EnsureSuccess();
                }
            }
        }

        /// <summary>
        /// Returns the NTP time sources to be used by the node.
        /// </summary>
        /// <returns>The quoted and space separated list of IP address or DNS hostnames for the node's NTP time sources in priority order.</returns>
        /// <remarks>
        /// <para>
        /// The cluster will be configured such that the first control-plane node (by sorted name) will be the primary timesource
        /// for the cluster.  All other control-plane and worker nodes will be configured to use the first control-plane node by default.
        /// Secondary control-plane nodes will be configured to use the external timesource next so any control-plane can automatically
        /// assume these duities.
        /// </para>
        /// <para>
        /// Worker nodes will be configured to use control-plane node in sorted order but will not be configured to use the 
        /// external time sources to avoid having large clusters spam the sources.
        /// </para>
        /// <para>
        /// The nice thing about this is that the cluster will almost always be closly synchronized with the first control-plane
        /// with graceful fallback on node failures.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when there is no associated cluster proxy.</exception>
        public string GetNtpSources()
        {
            var clusterDefinition  = Cluster.Definition;
            var nodeDefinition     = NodeDefinition;
            var sortedControlNodes = clusterDefinition.SortedControlNodes.ToArray();
            var firstControlNode   = sortedControlNodes.First();
            var sbExternalSources  = new StringBuilder();
            var sbNodeSources      = new StringBuilder();

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
                case NodeRole.ControlPlane:

                    if (nodeDefinition.Name.Equals(firstControlNode.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // The first control-plane is configured to use the external time sources only.

                        sbNodeSources.AppendWithSeparator(sbExternalSources.ToString());
                    }
                    else
                    {
                        // The remaining control-plane nodes are configured to prioritize the first control-plane and
                        // then fallback to the external sources.

                        sbNodeSources.AppendWithSeparator($"\"{firstControlNode.Address}\"");
                        sbNodeSources.AppendWithSeparator(sbExternalSources.ToString());
                    }
                    break;

                case NodeRole.Worker:

                    // Workers are configured to priortize the first control-plane and then fall
                    // back to the remaining control-plane nodes.  Workers will not be configured to
                    // use the external time sources to avoid having large clusters spam
                    // the sources.

                    foreach (var controlNode in sortedControlNodes)
                    {
                        sbNodeSources.AppendWithSeparator($"\"{controlNode.Address}\"");
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
        /// <para>
        /// Returns a clone of the SSH proxy.  This can be useful for situations where you
        /// need to be able to perform multiple SSH/SCP operations against the same
        /// machine in parallel.
        /// </para>
        /// <note>
        /// This does not clone any attached log writer.
        /// </note>
        /// </summary>
        /// <returns>The cloned <see cref="NodeSshProxy{TMetadata}"/>.</returns>
        public new NodeSshProxy<TMetadata> Clone()
        {
            var clone = new NodeSshProxy<TMetadata>(Name, Address, credentials, role: Role, port: SshPort);

            CloneTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        public bool GetIdempotentState(string actionId)
        {
            return FileExists(LinuxPath.Combine(KubeNodeFolder.State, actionId));
        }

        /// <inheritdoc/>
        public void SetIdempotentState(string actionId)
        {
            SudoCommand($"mkdir -p {KubeNodeFolder.State} && touch {KubeNodeFolder.State}/{actionId}", RunOptions.FaultOnError);
        }

        /// <inheritdoc/>
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

            var stateFolder = KubeNodeFolder.State;
            var slashPos = actionId.LastIndexOf('/');

            if (slashPos != -1)
            {
                // Extract any folder path from the activity ID and add it to
                // the state folder path.

                stateFolder = LinuxPath.Combine(stateFolder, actionId.Substring(0, slashPos));
                actionId    = actionId.Substring(slashPos + 1);

                Covenant.Assert(actionId.Length > 0);
            }

            var statePath = LinuxPath.Combine(stateFolder, actionId);

            SudoCommand($"mkdir -p {stateFolder}", RunOptions.FaultOnError);

            if (FileExists(statePath))
            {
                return false;
            }

            action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}", RunOptions.FaultOnError);
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> InvokeIdempotentAsync(string actionId, Func<Task> action)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId), nameof(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId), nameof(actionId));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            var stateFolder = KubeNodeFolder.State;
            var slashPos    = actionId.LastIndexOf('/');

            if (slashPos != -1)
            {
                // Extract any folder path from the activity ID and add it to
                // the state folder path.

                stateFolder = LinuxPath.Combine(stateFolder, actionId.Substring(0, slashPos));
                actionId = actionId.Substring(slashPos + 1);

                Covenant.Assert(actionId.Length > 0);
            }

            var statePath = LinuxPath.Combine(stateFolder, actionId);

            SudoCommand($"mkdir -p {stateFolder}", RunOptions.FaultOnError);

            if (FileExists(statePath))
            {
                return false;
            }

            await action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}", RunOptions.FaultOnError);
            }

            return true;
        }

        /// <summary>
        /// Ensures that the node operating system and version is supported for a neonKUBE
        /// cluster.  This faults the node proxy on failure.
        /// </summary>
        /// <param name="controller">Optional setup controller.</param>
        /// <returns><c>true</c> if the operation system is supported.</returns>
        public bool VerifyNodeOS(ISetupController controller = null)
        {
            if (controller != null)
            {
                controller.LogProgress(this, verb: "check", message: "operating system");
            }

            if (!OsName.Equals("Ubuntu", StringComparison.InvariantCultureIgnoreCase) || OsVersion != Version.Parse("22.04"))
            {
                Fault("Expected: Ubuntu 22.04");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cleans a node by removing unnecessary package manager metadata, cached DHCP information, journald
        /// logs... and then fills unreferenced file system blocks with zeros so the disk image will or
        /// trims the file system (when possible) so the image will compress better.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void Clean(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            controller.LogProgress(this, verb: "clean", message: "file system");

            var trim = HostingManager.SupportsFsTrim(hostingEnvironment);
            var zero = HostingManager.SupportsFsZero(hostingEnvironment);

            Clean(trim: trim, zero: zero);
        }

        /// <summary>
        /// Upgrades the base Linux distribtion, rebooting the node when required.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="fullUpgrade">
        /// Pass <c>true</c> to perform a full distribution upgrade or <c>false</c> to just 
        /// apply security patches.
        /// </param>
        public void UpdateLinux(ISetupController controller, bool fullUpgrade)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var nodeDefinition = NeonHelper.CastTo<NodeDefinition>(Metadata);

            InvokeIdempotent($"setup/upgrade-linux",
                () =>
                {
                    controller.LogProgress(this, verb: "upgrade", message: $"linux [full={fullUpgrade}]");

                    // Upgrade Linux packages if requested.

                    bool rebootRequired;

                    if (fullUpgrade)
                    {
                        Status         = "upgrade: full";
                        rebootRequired = PatchLinux();
                    }
                    else
                    {
                        Status         = "upgrade: partial";
                        rebootRequired = UpgradeLinuxDistribution();
                    }

                    // Check to see whether the upgrade requires a reboot and
                    // do that now if necessary.

                    if (rebootRequired)
                    {
                        Status = "restarting...";
                        Reboot();
                    }

                    // Clean up any cached APT files.

                    Status = "clean up";
                    SudoCommand($"{KubeNodeFolder.Bin}/safe-apt-get clean -yq");
                    SudoCommand("rm -rf /var/lib/apt/lists");
                });
        }

        /// <summary>
        /// <para>
        /// Manually sets whether SSH login using a password is enabled. 
        /// </para>
        /// </summary>
        /// <param name="enabled">
        /// Pass <c>true</c> to enable login using a password, or false to disable.
        /// </param>        
        public void SetSshPasswordLogin(bool enabled)
        {
            var script =
$@"
set -euo pipefail

if [ -f ""/etc/ssh/sshd_config.d/50-neonkube.conf"" ]; 
then     

    sed -iE 's/#*PasswordAuthentication.*/PasswordAuthentication {(enabled ? "yes" : "no")}/' /etc/ssh/sshd_config.d/50-neonkube.conf

    sed -iE 's/^PasswordAuthentication.*/#PasswordAuthentication {(enabled ? "yes" : "no")}/' /etc/ssh/sshd_config

else

    sed -iE 's/#*PasswordAuthentication.*/PasswordAuthentication {(enabled ? "yes" : "no")}/' /etc/ssh/sshd_config

fi
";

            SudoCommand(CommandBundle.FromScript(script), RunOptions.FaultOnError);

            SudoCommand("systemctl", "restart", "sshd", RunOptions.FaultOnError);
        }

        //---------------------------------------------------------------------
        // Override the base log related methods and write to the internal logs
        // as well.

        /// <summary>
        /// Returns the current log for the node.
        /// </summary>
        /// <returns>A <see cref="NodeLog"/>.</returns>
        public NodeLog GetLog()
        {
            return new NodeLog(Name, internalLogBuilder.ToString());
        }

        /// <inheritdoc/>
        public override void Log(string text)
        {
            base.Log(text);
            internalLogWriter.Write(text);

        }

        /// <inheritdoc/>
        public override void LogLine(string text)
        {
            base.LogLine(text);
            internalLogWriter.WriteLine(text);
        }

        /// <inheritdoc/>
        public override void LogFlush()
        {
            base.LogFlush();
            internalLogWriter.Flush();
        }
    }
}