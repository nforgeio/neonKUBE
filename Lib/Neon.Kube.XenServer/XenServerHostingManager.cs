//-----------------------------------------------------------------------------
// FILE:	    XenServerHostingManager.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Collections;
using Neon.Common;
using Neon.Net;
using Neon.XenServer;
using Neon.IO;
using Neon.SSH;

using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on the XenServer hypervisor.
    /// </summary>
    [HostingProvider(HostingEnvironment.XenServer)]
    public partial class XenServerHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to persist information about downloaded XVA template files.
        /// </summary>
        public class DiskTemplateInfo
        {
            /// <summary>
            /// The downloaded file ETAG.
            /// </summary>
            [JsonProperty(PropertyName = "ETag", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [YamlMember(Alias = "etag", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string ETag { get; set; }

            /// <summary>
            /// The downloaded file length used as a quick verification that
            /// the complete file was downloaded.
            /// </summary>
            [JsonProperty(PropertyName = "Length", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [YamlMember(Alias = "length", ApplyNamingConventions = false)]
            [DefaultValue(-1)]
            public long Length { get; set; }
        }

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
        // Instance members

        private ClusterProxy                cluster;
        private SetupController<XenClient>  xenController;
        private string                      driveTemplatePath;
        private string                      logFolder;
        private List<XenClient>             xenHosts;
        private int                         maxVmNameWidth;
        private string                      secureSshPassword;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public XenServerHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on XenServer/XCP-ng servers.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public XenServerHostingManager(ClusterProxy cluster, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            this.cluster                = cluster;
            this.cluster.HostingManager = this;
            this.logFolder              = logFolder;
            this.maxVmNameWidth         = cluster.Definition.Nodes.Max(node => node.Name.Length) + cluster.Definition.Hosting.Vm.GetVmNamePrefix(cluster.Definition).Length;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (xenHosts != null)
                {
                    foreach (var xenHost in xenHosts)
                    {
                        xenHost.Dispose();
                    }

                    xenHosts = null;
                }

                GC.SuppressFinalize(this);
            }

            xenHosts = null;
        }

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.XenServer;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => true;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(cluster != null, $"[{nameof(XenServerHostingManager)}] was created with the wrong constructor.");

            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            this.secureSshPassword = clusterLogin.SshPassword;

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // We'll call this to be consistent with the cloud hosting managers even though
            // the upstream on-premise router currently needs to be configured manually.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // We need to ensure that at least one node will host the OpenEBS
            // cStor block device.

            KubeHelper.EnsureOpenEbsNodes(cluster.Definition);

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in cluster.Definition.Nodes)
            {
                node.Labels.PhysicalMachine = node.Vm.Host;
                node.Labels.ComputeCores    = node.Vm.GetCores(cluster.Definition);
                node.Labels.ComputeRam      = (int)(node.Vm.GetMemory(cluster.Definition) / ByteUnits.MebiBytes);
                node.Labels.StorageSize     = ByteUnits.ToGiB(node.Vm.GetOsDisk(cluster.Definition));
            }

            // Build a list of [LinuxSshProxy] instances that map to the specified XenServer
            // hosts.  We'll use the [XenClient] instances as proxy metadata.  Note that we're
            // doing this to take advantage of [SetupController] to manage parallel operations
            // but we're not going to ever connect to XenServers via [LinuxSshProxy] and will
            // use [XenClient] ability to execute remote commands either via a local [xe-cli]
            // or via the XenServer API (in the future).

            var xenSshProxies = new List<NodeSshProxy<XenClient>>();

            xenHosts = new List<XenClient>();

            foreach (var host in cluster.Definition.Hosting.Vm.Hosts)
            {
                var hostAddress  = host.Address;
                var hostname     = host.Name;
                var hostUsername = host.Username ?? cluster.Definition.Hosting.Vm.HostUsername;
                var hostPassword = host.Password ?? cluster.Definition.Hosting.Vm.HostPassword;

                if (string.IsNullOrEmpty(hostname))
                {
                    hostname = host.Address;
                }

                var sshProxy = new NodeSshProxy<XenClient>(hostname, NetHelper.ParseIPv4Address(hostAddress), SshCredentials.FromUserPassword(hostUsername, hostPassword)); 
                var xenHost  = new XenClient(hostAddress, hostUsername, hostPassword, name: host.Name, logFolder: logFolder);

                xenHosts.Add(xenHost);

                sshProxy.Metadata = xenHost;
                xenSshProxies.Add(sshProxy);
            }

            // We're going to provision the XenServer hosts in parallel to
            // speed up cluster setup.  This works because each XenServer
            // is essentially independent from the others.

            xenController = new SetupController<XenClient>($"Provisioning [{cluster.Definition.Name}] cluster", xenSshProxies)
            {
                MaxParallel = this.MaxParallel
            };

            xenController.AddWaitUntilOnlineStep();
            xenController.AddNodeStep("verify readiness", (controller, node) => VerifyReady(node));
            xenController.AddNodeStep("virtual machine template", (controller, node) => CheckVmTemplate(node), parallelLimit: 1);
            xenController.AddNodeStep("create virtual machines", (controller, node) => ProvisionVM(node));

            controller.AddControllerStep(xenController);
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> setupController)
        {
            // We need to add any required OpenEBS cStor disks after the node has been otherwise
            // prepared.  We need to do this here because if we created the data and OpenEBS disks
            // when the VM is initially created, the disk setup scripts executed during prepare
            // won't be able to distinguish between the two disks.
            //
            // At this point, the data disk should be partitioned, formatted, and mounted so
            // the OpenEBS disk will be easy to identify as the only unpartitioned disk.

            // IMPLEMENTATION NOTE:
            // --------------------
            // This is a bit tricky.  The essential problem is that the setup controller passed
            // is intended for parallel operations on nodes, not XenServer hosts (like we did
            // above for provisioning).  We still have those XenServer host clients in the [xenHosts]
            // list field.  Note that XenClients are not thread-safe.
            // 
            // We're going to perform these operations in parallel, but require that each node
            // operation acquire a lock on the XenClient for the node's host before proceeding.

            setupController.AddNodeStep("openebs",
                (state, node) =>
                {
                    var xenClient = xenHosts.Single(client => client.Name == node.Metadata.Vm.Host);

                    node.Status = "openebs: waiting for host...";

                    lock (xenClient)
                    {
                        var vm = xenClient.Machine.List().Single(vm => vm.NameLabel == GetVmName(node));

                        if (xenClient.Machine.DiskCount(vm) < 2)
                        {
                            // We haven't created the cStor disk yet.

                            var disk = new XenVirtualDisk()
                            {
                                Name        = $"{GetVmName(node)}: openebs",
                                Size        = node.Metadata.Vm.GetOpenEbsDisk(cluster.Definition),
                                Description = "OpenEBS cStor"
                            };

                            node.Status = "openebs: stop VM";
                            xenClient.Machine.Shutdown(vm);

                            node.Status = "openebs: add cStor disk";
                            xenClient.Machine.AddDisk(vm, disk);

                            node.Status = "openebs: restart VM";
                            xenClient.Machine.Start(vm);
                        }
                    }
                },
                (state, node) => node.Metadata.OpenEBS);
        }

        /// <inheritdoc/>
        public override void AddDeprovisoningSteps(SetupController<NodeDefinition> controller)
        {
            // Deprovisioning is easy for XenServer.  All we need to do is to turn off
            // and remove the virtual machines.

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the list of <see cref="NodeDefinition"/> instances describing which cluster
        /// nodes are to be hosted by a specific XenServer.
        /// </summary>
        /// <param name="xenHost">The target XenServer.</param>
        /// <returns>The list of nodes to be hosted on the XenServer.</returns>
        private List<NodeSshProxy<NodeDefinition>> GetHostedNodes(XenClient xenHost)
        {
            var nodeDefinitions = cluster.Definition.NodeDefinitions.Values;

            return cluster.Nodes.Where(node => node.Metadata.Vm.Host.Equals(xenHost.Name, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns the name to use when naming the virtual machine hosting the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeSshProxy<NodeDefinition> node)
        {
            return $"{cluster.Definition.Hosting.Vm.GetVmNamePrefix(cluster.Definition)}{node.Name}";
        }

        /// <summary>
        /// Verify that the XenServer is ready to provision the cluster virtual machines.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private void VerifyReady(NodeSshProxy<XenClient> xenSshProxy)
        {
            // $todo(jefflill):
            //
            // It would be nice to verify that XenServer actually has enough 
            // resources (RAM, DISK, and perhaps CPU) here as well.

            var xenHost = xenSshProxy.Metadata;
            var nodes   = GetHostedNodes(xenHost);

            xenSshProxy.Status = "check: virtual machines";

            var vmNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vm in xenHost.Machine.List())
            {
                vmNames.Add(vm.NameLabel);
            }

            foreach (var hostedNode in nodes)
            {
                var vmName = GetVmName(hostedNode);

                if (vmNames.Contains(vmName))
                {
                    xenSshProxy.Fault($"XenServer [{xenHost.Name}] already hosts a virtual machine named [{vmName}].");
                    return;
                }
            }
        }

        /// <summary>
        /// Install the virtual machine template on the XenServer if it's not already present.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private void CheckVmTemplate(NodeSshProxy<XenClient> xenSshProxy)
        {
            var xenHost      = xenSshProxy.Metadata;
            var templateName = $"neonkube-{KubeVersions.NeonKubeVersion}";
            var nodeImageUri = KubeDownloads.GetNodeImageUri(this.HostingEnvironment, xenController);

            xenSshProxy.Status = "check: template";

            if (xenHost.Template.Find(templateName) == null)
            {
                xenSshProxy.Status = "download: vm template (slow)";

                // Download the GZIPed XVA template if it's not already present.  Note that we're 
                // going to name the file the same as the file name from the URI and also that 
                // templates are considered to be invariant.

                var driveTemplateUri  = new Uri(KubeDownloads.GetNodeImageUri(this.HostingEnvironment, xenController));
                var driveTemplateName = driveTemplateUri.Segments.Last();

                driveTemplatePath = Path.Combine(KubeHelper.NodeImageFolder, driveTemplateName);

                if (!File.Exists(driveTemplatePath))
                {
                    xenController.SetGlobalStepStatus($"Download node image VHDX: [{nodeImageUri}]");

                    Task.Run(
                        async () =>
                        {
                            using (var client = new HttpClient())
                            {
                                // Download the file.

                                var response = await client.GetAsync(nodeImageUri, HttpCompletionOption.ResponseHeadersRead);

                                response.EnsureSuccessStatusCode();

                                var contentLength   = response.Content.Headers.ContentLength;
                                var contentEncoding = response.Content.Headers.ContentEncoding.SingleOrDefault();

                                if (string.IsNullOrEmpty(contentEncoding) || !contentEncoding.Equals("gzip", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    throw new KubeException($"[{nodeImageUri}] has unsupported [Content-Encoding={contentEncoding}].  Expecting [gzip]");
                                }

                                try
                                {
                                    using (var fileStream = new FileStream(driveTemplatePath, FileMode.Create, FileAccess.ReadWrite))
                                    {
                                        using (var downloadStream = await response.Content.ReadAsStreamAsync())
                                        {
                                            var buffer = new byte[64 * 1024];
                                            int cb;

                                            while (true)
                                            {
                                                cb = await downloadStream.ReadAsync(buffer, 0, buffer.Length);

                                                if (cb == 0)
                                                {
                                                    break;
                                                }

                                                await fileStream.WriteAsync(buffer, 0, cb);

                                                if (contentLength.HasValue)
                                                {
                                                    var percentComplete = (int)(((double)fileStream.Length / (double)contentLength) * 100.0);

                                                    xenController.SetGlobalStepStatus($"Downloading VHDX: [{percentComplete}%] [{nodeImageUri}]");
                                                }
                                                else
                                                {
                                                    xenController.SetGlobalStepStatus($"Downloading VHDX: [{fileStream.Length} bytes] [{nodeImageUri}]");
                                                }
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ensure that the template file is are deleted if there were any
                                    // errors to help avoid using a corrupted template.

                                    if (File.Exists(driveTemplatePath))
                                    {
                                        File.Delete(driveTemplatePath);
                                    }

                                    throw;
                                }
                            }

                        }).Wait();
                }

                xenController.SetGlobalStepStatus();
                xenSshProxy.Status = "install: virtual machine template (slow)";
                xenHost.Template.ImportVmTemplate(driveTemplatePath, templateName, cluster.Definition.Hosting.XenServer.StorageRepository);
            }
        }

        /// <summary>
        /// Formats a nice node status message.
        /// </summary>
        /// <param name="vmName">The name of the virtual machine used to host the cluster node.</param>
        /// <param name="message">The status message.</param>
        /// <returns>The formatted status message.</returns>
        private string FormatVmStatus(string vmName, string message)
        {
            var namePart     = $"[{vmName}]:";
            var desiredWidth = maxVmNameWidth + 3;
            var actualWidth  = namePart.Length;

            if (desiredWidth > actualWidth)
            {
                namePart += new string(' ', desiredWidth - actualWidth);
            }

            return $"{namePart} {message}";
        }

        /// <summary>
        /// Provision the virtual machines on the XenServer.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private void ProvisionVM(NodeSshProxy<XenClient> xenSshProxy)
        {
            var xenHost  = xenSshProxy.Metadata;
            var hostInfo = xenHost.GetHostInfo();

            if (hostInfo.Version < KubeConst.MinXenServerVersion)
            {
                throw new NotSupportedException($"neonKUBE cannot provision a cluster on a XenServer/XCP-ng host older than [v{KubeConst.MinXenServerVersion}].  [{hostInfo.Params["name-label"]}] is running version [{hostInfo.Version}]. ");
            }

            foreach (var node in GetHostedNodes(xenHost))
            {
                var vmName      = GetVmName(node);
                var cores       = node.Metadata.Vm.GetCores(cluster.Definition);
                var memoryBytes = node.Metadata.Vm.GetMemory(cluster.Definition);
                var osDiskBytes = node.Metadata.Vm.GetOsDisk(cluster.Definition);

                xenSshProxy.Status = FormatVmStatus(vmName, "create: virtual machine");

                var vm = xenHost.Machine.Create(vmName, $"neonkube-{KubeVersions.NeonKubeVersion}",
                    cores:                      cores,
                    memoryBytes:                memoryBytes,
                    diskBytes:                  osDiskBytes,
                    snapshot:                   cluster.Definition.Hosting.XenServer.Snapshot,
                    primaryStorageRepository:   cluster.Definition.Hosting.XenServer.StorageRepository);;

                // Create a temporary ISO with the [neon-init.sh] script, mount it
                // to the VM and then boot the VM for the first time.  The script on the
                // ISO will be executed automatically by the [neon-init] service
                // preinstalled on the VM image and the script will configure the secure 
                // SSH password and then the network.
                //
                // This ensures that SSH is not exposed to the network before the secure
                // password has been set.

                var tempIso    = (TempFile)null;
                var xenTempIso = (XenTempIso)null;

                try
                {
                    // Create a temporary ISO with the prep script and insert it
                    // into the node VM.

                    node.Status = $"mount: neon-init iso";

                    tempIso    = KubeHelper.CreateNeonInitIso(node.Cluster.Definition, node.Metadata, secureSshPassword);
                    xenTempIso = xenHost.CreateTempIso(tempIso.Path);

                    xenHost.Invoke($"vm-cd-eject", $"uuid={vm.Uuid}");
                    xenHost.Invoke($"vm-cd-insert", $"uuid={vm.Uuid}", $"cd-name={xenTempIso.IsoName}");

                    // Start the VM for the first time with the mounted ISO.  The network
                    // configuration will happen automatically by the time we can connect.

                    node.Status = $"start: virtual machine (first boot)";
                    xenHost.Machine.Start(vm);

                    // Update the node credentials to use the secure password and then wait for the node to boot.

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, secureSshPassword));

                    node.Status = $"connecting...";
                    node.WaitForBoot();

                    // Extend the primary partition and file system to fill 
                    // the virtual disk.  Note that we're not going to do
                    // this if the specified disk size is less than or equal
                    // to the node template's disk size (because that
                    // would fail).
                    //
                    // Note that there should only be one unpartitioned disk at
                    // this point: the OS disk.

                    var partitionedDisks = node.ListPartitionedDisks();
                    var osDisk           = partitionedDisks.Single();

                    if (osDiskBytes > KubeConst.MinNodeDiskSizeGiB)
                    {
                        node.Status = $"resize: OS disk";
                        node.SudoCommand($"growpart {osDisk} 2", RunOptions.None);
                        node.SudoCommand($"resize2fs {osDisk}2", RunOptions.None);
                    }
                }
                finally
                {
                    // Be sure to delete the local and remote ISO files so these don't accumulate.

                    tempIso?.Dispose();

                    // These can also accumulate on the XenServer.

                    if (xenTempIso != null)
                    {
                        xenHost.Invoke($"vm-cd-eject", $"uuid={vm.Uuid}");
                        xenHost.RemoveTempIso(xenTempIso);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override string GetDataDisk(LinuxSshProxy node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            // This hosting manager doesn't currently provision a separate data disk.

            return "PRIMARY";
        }
    }
}
