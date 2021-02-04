//-----------------------------------------------------------------------------
// FILE:	    Wsl2HostingManager.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on the local workstation using Microsoft Hyper-V virtual machines.
    /// This is typically used for development and test purposes.
    /// </summary>
    [HostingProvider(HostingEnvironment.Wsl2)]
    public class Wsl2HostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        //---------------------------------------------------------------------
        // Instance members.

        private ClusterProxy    cluster;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public Wsl2HostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on the local machine using Hyper-V.
        /// </summary>
        /// <param name="cluster">The cluster being managed.
        /// </param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public Wsl2HostingManager(ClusterProxy cluster, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager = this;

            this.cluster = cluster;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override bool IsProvisionNOP => false;

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.Wsl2;

        // NOTE:
        //
        // We're going to leave the default password for WSL2 distros to make them
        // easier for users to manage.  This isn't a security gap because the OpenSSH
        // service will only be reachable from the Windows host machine via the internal
        // [127.x.x.x] address.  This will not be reachable from the LAN.

        /// <inheritdoc/>
        public override bool GenerateSecurePassword => false;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
        }

        /// <inheritdoc/>
        public override async Task<bool> ProvisionAsync(ClusterLogin clusterLogin, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(clusterLogin != null, nameof(clusterLogin));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword), nameof(secureSshPassword));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(orgSshPassword), nameof(orgSshPassword));
            Covenant.Assert(cluster != null, $"[{nameof(Wsl2HostingManager)}] was created with the wrong constructor.");
            Covenant.Assert(cluster.Definition.Nodes.Count() == 1, "WSL2 clusters may only include a single node.");
            Covenant.Assert(Wsl2Proxy.IsWsl2Enabled, "WSL2 must be enabled before a WSL cluster can be provisioned.");

            // These define where the node image is downloaded from as well as where it 
            // will downloaded on the local workstation.

            var nodeImageUri  = $"https://neonkube.s3-us-west-2.amazonaws.com/images/wsl2/node/ubuntu-20.04.{KubeVersions.NeonKubeVersion}.tar";
            var nodeImagePath = Path.Combine(KubeHelper.NodeImageFolder, $"{KubeVersions.NeonKubeVersion}.tar");

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // We need to ensure that at least one node will host the OpenEBS
            // cStor block device.

            KubeHelper.EnsureOpenEbsNodes(cluster.Definition);

            // Initialize and run the [SetupController].

            var operation       = $"Provisioning [{cluster.Definition.Name}] on WSL2";
            var setupController = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus     = this.ShowStatus,
                ShowNodeStatus = true,
                MaxParallel    = int.MaxValue       // There's no reason to constrain this
            };

            setupController.AddGlobalStep($"Download neonDESKTOP/WSL2 distro",
                async state =>
                {
                    if (!File.Exists(nodeImagePath))
                    {
                        try
                        {
                            using (var httpClient = new HttpClient())
                            {
                                var response        = await httpClient.GetToFileSafeAsync(nodeImageUri, nodeImagePath);
                                var contentEncoding = response.Content.Headers.ContentEncoding.SingleOrDefault();

                                if (string.IsNullOrEmpty(contentEncoding) || !contentEncoding.Equals("gzip", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    throw new KubeException($"[{nodeImageUri}] has unsupported [Content-Encoding={contentEncoding}].  Expecting [gzip].");
                                }
                            }
                        }
                        catch
                        {
                            // Delete partially downloaded files.

                            NeonHelper.DeleteFile(nodeImagePath);
                            throw;
                        }
                    }
                });

            setupController.AddGlobalStep($"Create WSL2 [{KubeConst.Wsl2MainDistroName}] distro",
                state =>
                {
                    if (Wsl2Proxy.Exists(KubeConst.Wsl2MainDistroName))
                    {
                        Wsl2Proxy.Unregister(KubeConst.Wsl2MainDistroName);
                    }

                    Wsl2Proxy.Import(KubeConst.Wsl2MainDistroName, nodeImagePath, KubeHelper.DesktopWsl2Folder);

                    var distro = new Wsl2Proxy(KubeConst.Wsl2MainDistroName, KubeConst.SysAdminUser);

                    distro.Start();
                });

            setupController.AddNodeStep("Connect", (state, node) => node.WaitForBoot(timeout: TimeSpan.FromMinutes(4)));

            setupController.AddNodeStep("credentials",
                (state, node) =>
                {
                    // Update the node SSH proxies to use the secure SSH password.

                    node.RunCommand(CommandBundle.FromScript($"echo \"{secureSshPassword}\" | chpasswd"));
                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, secureSshPassword));
                },
                quiet: true);

            if (!setupController.Run(leaveNodesConnected: false))
            {
                Console.WriteLine("*** One or more WSL2 provisioning steps failed.");
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override void AddPostPrepareSteps(SetupController<NodeDefinition> setupController)
        {
            // Nothing to do here.
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            var distro = new Wsl2Proxy(KubeConst.Wsl2MainDistroName, KubeConst.SysAdminUser);

            return (Address: distro.Address, Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => false;

        /// <inheritdoc/>
        public override string GetDataDisk(LinuxSshProxy node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            // This hosting manager doesn't currently provision a separate data disk.

            return "PRIMARY";
        }
    }
}
