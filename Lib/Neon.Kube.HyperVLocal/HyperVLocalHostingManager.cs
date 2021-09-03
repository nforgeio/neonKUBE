//-----------------------------------------------------------------------------
// FILE:	    HyperVLocalHostingManager.cs
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
using Neon.HyperV;
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
    [HostingProvider(HostingEnvironment.HyperVLocal)]
    public class HyperVLocalHostingManager : HostingManager
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

        private const string defaultSwitchName = "external";

        private ClusterProxy                        cluster;
        private string                              nodeImageUri;
        private string                              nodeImagePath;
        private SetupController<NodeDefinition>     controller;
        private string                              driveTemplatePath;
        private string                              vmDriveFolder;
        private LocalHyperVHostingOptions           hostingOptions;
        private string                              switchName;
        private string                              secureSshPassword;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public HyperVLocalHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on the local machine using Hyper-V.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="nodeImageUri">Optionally specifies the node image URI (one of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be passed).</param>
        /// <param name="nodeImagePath">Optionally specifies the path to the local node image file (one of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be passed).</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <remarks>
        /// <note>
        /// One of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified.
        /// </note>
        /// </remarks>
        public HyperVLocalHostingManager(ClusterProxy cluster, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager = this;

            this.cluster        = cluster;
            this.nodeImageUri   = nodeImageUri;
            this.nodeImagePath  = nodeImagePath;
            this.hostingOptions = cluster.Definition.Hosting.HyperVLocal;

            // Determine where we're going to place the VM hard drive files and
            // ensure that the directory exists.

            if (!string.IsNullOrEmpty(cluster.Definition.Hosting.Vm.DiskLocation))
            {
                vmDriveFolder = cluster.Definition.Hosting.Vm.DiskLocation;
            }
            else
            {
                vmDriveFolder = HyperVClient.DefaultDriveFolder;
            }

            Directory.CreateDirectory(vmDriveFolder);
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
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.HyperVLocal;

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
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVLocalHostingManager)}] was created with the wrong constructor.");

            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            this.controller        = controller;
            this.secureSshPassword = clusterLogin.SshPassword;

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
                node.Labels.PhysicalMachine = Environment.MachineName;
                node.Labels.ComputeCores    = cluster.Definition.Hosting.Vm.Cores;
                node.Labels.ComputeRam      = (int)(ClusterDefinition.ValidateSize(cluster.Definition.Hosting.Vm.Memory, typeof(HostingOptions), nameof(HostingOptions.Vm.Memory))/ ByteUnits.MebiBytes);
                node.Labels.StorageSize     = ByteUnits.ToGiB(node.Vm.GetMemory(cluster.Definition));
            }

            // Add the provisioning steps to the controller.

            controller.MaxParallel = 1; // We're only going to provision one VM at a time on the local Hyper-V.

            controller.AddGlobalStep("initialize",
                controller =>
                {
                    var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

                    this.secureSshPassword = clusterLogin.SshPassword;
                });

            if (!controller.Get<bool>(KubeSetupProperty.DisableImageDownload, false))
            {
                controller.AddGlobalStep($"hyper-v node image",
                    async state =>
                    {
                        // Download the GZIPed VHDX template if it's not already present and has a valid
                        // MD5 hash file.
                        //
                        // Note that we're going to name the file the same as the file name from the URI.

                        string driveTemplateName;

                        if (!string.IsNullOrEmpty(nodeImageUri))
                        {
                            var driveTemplateUri = new Uri(nodeImageUri);

                            driveTemplateName = driveTemplateUri.Segments.Last();
                            driveTemplatePath = Path.Combine(KubeHelper.NodeImageFolder, driveTemplateName);

                            await KubeHelper.DownloadNodeImageAsync(nodeImageUri, driveTemplatePath,
                                (type, progress) =>
                                {
                                    controller.SetGlobalStepStatus($"{type} VHDX: [{progress}%] [{driveTemplateName}]");

                                    return !controller.CancelPending;
                                });
                        }
                        else
                        {
                            Covenant.Assert(File.Exists(nodeImagePath), $"Missing file: {nodeImagePath}");

                            driveTemplateName = Path.GetFileName(nodeImagePath);
                            driveTemplatePath = nodeImagePath;
                        }

                        controller.SetGlobalStepStatus();
                    });
            }

            controller.AddGlobalStep("configure hyper-v", async controller => await PrepareHyperVAsync());
            controller.AddNodeStep("create virtual machines", (controller, node) => ProvisionVM(node));
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            // We need to add any required OpenEBS cStor disks after the node has been otherwise
            // prepared.  We need to do this here because if we created the data and OpenEBS disks
            // when the VM is initially created, the disk setup scripts executed during prepare
            // won't be able to distinguish between the two disks.
            //
            // At this point, the data disk should be partitioned, formatted, and mounted so
            // the OpenEBS disk will be easy to identify as the only unpartitioned disk.

            controller.AddNodeStep("openebs",
                (state, node) =>
                {
                    using (var hyperv = new HyperVClient())
                    {
                        var vmName   = GetVmName(node.Metadata);
                        var diskSize = node.Metadata.Vm.GetOpenEbsDisk(cluster.Definition);
                        var diskPath = Path.Combine(vmDriveFolder, $"{vmName}-openebs.vhdx");

                        node.Status = "openebs: checking";

                        if (hyperv.GetVmDrives(vmName).Count < 2)
                        {
                            // The disk doesn't already exist.

                            node.Status = "openebs: stop VM";
                            hyperv.StopVm(vmName);

                            node.Status = "openebs: add cStor disk";
                            hyperv.AddVmDrive(vmName,
                                new VirtualDrive()
                                {
                                    Path = diskPath,
                                    Size = diskSize
                                });

                            node.Status = "openebs: restart VM";
                            hyperv.StartVm(vmName);
                        }
                    }
                },
                (state, node) => node.Metadata.OpenEBS);
        }

        /// <inheritdoc/>
        public override void AddDeprovisoningSteps(SetupController<NodeDefinition> controller)
        {
            // Deprovisioning is easy for Hyper-V.  All we need to do is to turn off
            // and remove the virtual machines.

            controller.AddNodeStep("turn-off virtual machines",
                (controller, node) =>
                {
                    node.Status = "turning off";

                    using (var hyperv = new HyperVClient())
                    {
                        var vmName = GetVmName(node.Metadata);

                        hyperv.StopVm(vmName, turnOff: true);
                    }
                });

            controller.AddNodeStep("remove virtual machines",
                (controller, node) =>
                {
                    node.Status = "removing";

                    using (var hyperv = new HyperVClient())
                    {
                        var vmName = GetVmName(node.Metadata);

                        hyperv.RemoveVm(vmName);
                    }
                });
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => true;

        /// <inheritdoc/>
        public override string GetDataDisk(LinuxSshProxy node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            // This hosting manager doesn't currently provision a separate data disk.

            return "PRIMARY";
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine hosting the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            return $"{cluster.Definition.Hosting.Vm.GetVmNamePrefix(cluster.Definition)}{node.Name}";
        }

        /// <summary>
        /// Attempts to extract the cluster node name from a virtual machine name.
        /// </summary>
        /// <param name="machineName">The virtual machine name.</param>
        /// <returns>
        /// The extracted node name if the virtual machine belongs to this 
        /// cluster or else the empty string.
        /// </returns>
        private string ExtractNodeName(string machineName)
        {
            var clusterPrefix = cluster.Definition.Hosting.Vm.GetVmNamePrefix(cluster.Definition);

            if (machineName.StartsWith(clusterPrefix))
            {
                return machineName.Substring(clusterPrefix.Length);
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Performs any required Hyper-V initialization before cluster nodes can be provisioned.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task PrepareHyperVAsync()
        {
            if (string.IsNullOrEmpty(nodeImageUri) && string.IsNullOrEmpty(nodeImagePath))
            {
                throw new InvalidOperationException($"[{nameof(nodeImageUri)}] or [{nameof(nodeImagePath)}] was not passed to the hosting manager's constructor which is required for preparing a cluster.");
            }

            // Handle any necessary Hyper-V initialization.

            using (var hyperv = new HyperVClient())
            {
                // Manage the Hyper-V virtual switch.  This will be an internal switch
                // when [UseInternalSwitch=TRUE] otherwise this will be external.

                if (hostingOptions.UseInternalSwitch)
                {
                    switchName = KubeConst.HyperVLocalInternalSwitchName;

                    controller.SetGlobalStepStatus($"configure: [{switchName}] internal switch");

                    // We're going to create an internal switch named [neonkube] configured
                    // with the standard private subnet and a NAT to enable external routing.

                    var @switch = hyperv.GetSwitch(switchName);

                    if (@switch == null)
                    {
                        // The internal switch doesn't exist yet, so create it.  Note that
                        // this switch always includes an external NAT.

                        controller.SetGlobalStepStatus($"add: [{switchName}] internal switch with NAT for [{hostingOptions.NeonKubeInternalSubnet}]");
                        hyperv.NewInternalSwitch(switchName, hostingOptions.NeonKubeInternalSubnet, addNAT: true);
                        controller.SetGlobalStepStatus();
                    }
                    else
                    {
                        // The switch exists.  We need to ensure that it has an attached IP address and NAT.
                        // It's possible that these haven't been created yet when previous cluster deployments
                        // may have failed.

                        controller.SetGlobalStepStatus($"configure: [{switchName}] internal switch with NAT for [{hostingOptions.NeonKubeInternalSubnet}]");

                        // $todo(jefflill): Implement this

                        controller.SetGlobalStepStatus();
                    }
                }
                else
                {
                    // We're going to create an external Hyper-V switch if there
                    // isn't already an external switch.

                    controller.SetGlobalStepStatus("Scanning network adapters");

                    var externalSwitch = hyperv.ListSwitches().FirstOrDefault(@switch => @switch.Type == VirtualSwitchType.External);

                    if (externalSwitch == null)
                    {
                        hyperv.NewExternalSwitch(switchName = defaultSwitchName, NetHelper.ParseIPv4Address(cluster.Definition.Network.Gateway));
                    }
                    else
                    {
                        switchName = externalSwitch.Name;
                    }
                }

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                controller.SetGlobalStepStatus("Scanning virtual machines");

                var existingMachines = hyperv.ListVms();
                var conflicts        = string.Empty;

                controller.SetGlobalStepStatus("Stopping virtual machines");

                foreach (var machine in existingMachines)
                {
                    var nodeName    = ExtractNodeName(machine.Name);
                    var drivePath   = Path.Combine(vmDriveFolder, $"{machine.Name}.vhdx");
                    var isClusterVM = cluster.FindNode(nodeName) != null;

                    if (isClusterVM)
                    {
                        // We're going to report errors when one or more machines already exist.

                        if (conflicts.Length > 0)
                        {
                            conflicts += ", ";
                        }

                        conflicts += nodeName;
                    }
                }

                if (!string.IsNullOrEmpty(conflicts))
                {
                    throw new HyperVException($"[{conflicts}] virtual machine(s) already exists.");
                }

                controller.SetGlobalStepStatus();
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates a Hyper-V virtual machine for a cluster node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ProvisionVM(NodeSshProxy<NodeDefinition> node)
        {
            using (var hyperv = new HyperVClient())
            {
                var vmName = GetVmName(node.Metadata);

                // Decompress the VHDX template file to the virtual machine's
                // virtual hard drive file.

                var driveTemplateInfoPath = driveTemplatePath + ".info";
                var osDrivePath           = Path.Combine(vmDriveFolder, $"{vmName}.vhdx");

                using (var input = new FileStream(driveTemplatePath, FileMode.Open, FileAccess.Read))
                {
                    using (var output = new FileStream(osDrivePath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (var decompressor = new GZipStream(input, CompressionMode.Decompress))
                        {
                            var     buffer = new byte[64 * 1024];
                            long    cbRead = 0;
                            int     cb;

                            while (true)
                            {
                                cb     = decompressor.Read(buffer, 0, buffer.Length);
                                cbRead = input.Position;

                                if (cb == 0)
                                {
                                    break;
                                }

                                output.Write(buffer, 0, cb);

                                var percentComplete = (int)((double)cbRead / (double)input.Length * 100.0);

                                node.Status = $"decompress: VHDX [{percentComplete}%]";
                            }

                            node.Status = $"decompress: VHDX [100]";
                        }
                    }
                }

                // Create the virtual machine.

                var processors  = node.Metadata.Vm.GetCores(cluster.Definition);
                var memoryBytes = node.Metadata.Vm.GetMemory(cluster.Definition);
                var osDiskBytes = node.Metadata.Vm.GetOsDisk(cluster.Definition);

                node.Status = $"create: virtual machine";
                hyperv.AddVm(
                    vmName,
                    processorCount: processors,
                    diskSize:       osDiskBytes.ToString(),
                    memorySize:     memoryBytes.ToString(),
                    drivePath:      osDrivePath,
                    switchName:     switchName);

                // Create a temporary ISO with the [neon-init.sh] script, mount it
                // to the VM and then boot the VM for the first time.  The script on the
                // ISO will be executed automatically by the [neon-init] service
                // preinstalled on the VM image and the script will configure the secure 
                // SSH password and then the network.
                //
                // This ensures that SSH is not exposed to the network before the secure
                // password has been set.

                var tempIso = (TempFile)null;

                try
                {
                    // Create a temporary ISO with the prep script and mount it
                    // to the node VM.

                    node.Status = $"mount: neon-init iso";
                    tempIso     = KubeHelper.CreateNeonInitIso(node.Cluster.Definition, node.Metadata, secureSshPassword);

                    hyperv.InsertVmDvd(vmName, tempIso.Path);

                    // Start the VM for the first time with the mounted ISO.  The network
                    // configuration will happen automatically by the time we can connect.

                    node.Status = $"start: virtual machine (first boot)";
                    hyperv.StartVm(vmName);

                    // Update the node credentials to use the secure password and then wait for the node to boot.

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, secureSshPassword));
                    node.WaitForBoot();

                    // Extend the primary partition and file system to fill 
                    // the virtual drive.  Note that we're not going to do
                    // this if the specified drive size is less than or equal
                    // to the node template's drive size (because that
                    // would fail).
                    //
                    // Note that there should only be one partitioned disk at
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
                    // Be sure to delete the ISO file so these don't accumulate.

                    tempIso?.Dispose();
                }
            }
        }

        /// <summary>
        /// Perform any necessary global post Hyper-V provisioning steps.
        /// </summary>
        private void Finish()
        {
        }
    }
}
