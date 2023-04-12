//-----------------------------------------------------------------------------
// FILE:	    HyperVHostingManager.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Runtime.InteropServices;
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
using Neon.Kube.ClusterDef;
using Neon.Kube.Login;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;
using Neon.Windows;

namespace Neon.Kube.Hosting.HyperV
{
    /// <summary>
    /// Manages cluster provisioning using Microsoft Hyper-V virtual machines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional capability support:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="HostingCapabilities.Pausable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// <item>
    ///     <term><see cref="HostingCapabilities.Stoppable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// </list>
    /// </remarks>
    [HostingProvider(HostingEnvironment.HyperV)]
    public class HyperVHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to limit how many threads will be created by parallel operations.
        /// </summary>
        private static readonly ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = MaxAsyncParallelHostingOperations };

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
        private HyperVHostingOptions                hostingOptions;
        private string                              switchName;
        private string                              secureSshPassword;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public HyperVHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of managing and/or provisioning a cluster on the local machine using Hyper-V.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="cloudMarketplace">Ignored</param>
        /// <param name="nodeImageUri">Optionally specifies the node image URI.</param>
        /// <param name="nodeImagePath">Optionally specifies the path to the local node image file.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <remarks>
        /// <note>
        /// One of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified to be able
        /// to provision a cluster but these can be <c>null</c> when you need to manage a cluster lifecycle.
        /// </note>
        /// </remarks>
        public HyperVHostingManager(ClusterProxy cluster, bool cloudMarketplace, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager = this;

            this.cluster        = cluster;
            this.nodeImageUri   = nodeImageUri;
            this.nodeImagePath  = nodeImagePath;
            this.hostingOptions = cluster.SetupDetails.ClusterDefinition.Hosting.HyperV;

            // Determine where we're going to place the VM hard drive files and
            // ensure that the directory exists.

            if (!string.IsNullOrEmpty(cluster.SetupDetails.ClusterDefinition.Hosting.Vm.DiskLocation))
            {
                vmDriveFolder = cluster.SetupDetails.ClusterDefinition.Hosting.Vm.DiskLocation;
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
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.HyperV;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => true;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (clusterDefinition.Hosting.Environment != HostingEnvironment.HyperV)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.HyperV}].");
            }
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            this.controller        = controller;
            this.secureSshPassword = cluster.SetupDetails.SshPassword;

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.SetupDetails.ClusterDefinition);

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in cluster.SetupDetails.ClusterDefinition.Nodes)
            {
                node.Labels.PhysicalMachine = Environment.MachineName;
                node.Labels.ComputeCores    = cluster.SetupDetails.ClusterDefinition.Hosting.Vm.Cores;
                node.Labels.ComputeRam      = (int)(ClusterDefinition.ValidateSize(cluster.SetupDetails.ClusterDefinition.Hosting.Vm.Memory, typeof(HostingOptions), nameof(HostingOptions.Vm.Memory))/ ByteUnits.MebiBytes);
                node.Labels.StorageSize     = ByteUnits.ToGiB(node.Vm.GetMemory(cluster.SetupDetails.ClusterDefinition));
            }

            // Add the provisioning steps to the controller.

            controller.MaxParallel = 1; // We're only going to provision one VM at a time on the local Hyper-V.

            controller.AddGlobalStep("check hyper-v",
                controller =>
                {
                    this.secureSshPassword = cluster.SetupDetails.SshPassword;

                    // If the cluster is being deployed to the internal [neonkube] switch, we need to
                    // check to see whether the switch already exists, and if it does, we'll need to
                    // ensure that it's configured correctly with a virtual address and NAT.  We're
                    // going to fail setup when an existing switch isn't configured correctly.

                    if (cluster.SetupDetails.ClusterDefinition.Hosting.HyperV.UseInternalSwitch)
                    {
                        using (var hyperv = new HyperVProxy())
                        {
                            controller.SetGlobalStepStatus($"check: [{KubeConst.HyperVInternalSwitchName}] virtual switch/NAT");

                            var localHyperVOptions = cluster.SetupDetails.ClusterDefinition.Hosting.HyperV;
                            var @switch            = hyperv.FindSwitch(KubeConst.HyperVInternalSwitchName);
                            var address            = hyperv.FindIPAddress(localHyperVOptions.NeonDesktopNodeAddress.ToString());
                            var nat                = hyperv.FindNatByName(KubeConst.HyperVInternalSwitchName);

                            if (@switch != null)
                            {
                                if (@switch.Type != VirtualSwitchType.Internal)
                                {
                                    throw new NeonKubeException($"The existing [{@switch.Name}] Hyper-V virtual switch is misconfigured.  It's type must be [internal].");
                                }

                                if (address != null && !address.InterfaceName.Equals(@switch.Name))
                                {
                                    throw new NeonKubeException($"The existing [{@switch.Name}] Hyper-V virtual switch is misconfigured.  The [{address}] IP address is not assigned to this switch.");
                                }
                            }

                            if (nat != null && nat.Subnet != localHyperVOptions.NeonKubeInternalSubnet)
                            {
                                throw new NeonKubeException($"The existing [{@switch.Name}] Hyper-V virtual switch is misconfigured.  The [{nat.Name}] NAT subnet is not set to [{localHyperVOptions.NeonKubeInternalSubnet}].");
                            }
                        }
                    }
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

                            driveTemplateName = Path.GetFileNameWithoutExtension(driveTemplateUri.Segments.Last());
                            driveTemplatePath = Path.Combine(KubeHelper.VmImageFolder, driveTemplateName);

                            await KubeHelper.DownloadNodeImageAsync(nodeImageUri, driveTemplatePath,
                                (progressType, progress) =>
                                {
                                    controller.SetGlobalStepStatus($"{NeonHelper.EnumToString(progressType)}: VHDX [{progress}%] [{driveTemplateName}]");

                                    return !controller.IsCancelPending;
                                });
                        }
                        else
                        {
                            Covenant.Assert(File.Exists(nodeImagePath), $"Missing file: {nodeImagePath}");

                            driveTemplateName = Path.GetFileName(nodeImagePath);
                            driveTemplatePath = nodeImagePath;
                        }
                    });
            }
            else
            {
                Covenant.Assert(!string.IsNullOrEmpty(nodeImagePath), $"[{nameof(nodeImagePath)}] must be specified when node image download is disabled.");
                Covenant.Assert(File.Exists(nodeImagePath), $"Missing file: {nodeImagePath}");

                driveTemplatePath = nodeImagePath;
            }

            var typedController = (SetupController<NodeDefinition>)controller;
            var createVmLabel   = "create virtual machine";

            if (cluster.SetupDetails.ClusterDefinition.Nodes.Count() > 1)
            {
                createVmLabel += "(s)";
            }

            controller.AddGlobalStep("configure hyper-v", async controller => await PrepareHyperVAsync(typedController));
            controller.AddNodeStep(createVmLabel, (controller, node) => ProvisionVM(typedController, node));
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            if (cluster.SetupDetails.ClusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.cStor)
            {
                // We need to add any required OpenEBS cStor disks after the node has been otherwise
                // prepared.  We need to do this here because if we created the data and OpenEBS disks
                // at the same time when the VM is initially created, the disk setup scripts executed
                // during prepare won't be able to distinguish between the two disks.
                //
                // At this point, the data disk should be partitioned, formatted, and mounted so
                // the OpenEBS disk will be easy to identify as the only unpartitioned disk.

                controller.AddNodeStep("openebs",
                (controller, node) =>
                {
                    using (var hyperv = new HyperVProxy())
                    {
                        var vmName   = GetVmName(node.Metadata);
                        var diskSize = node.Metadata.Vm.GetOpenEbsDiskSizeBytes(cluster.SetupDetails.ClusterDefinition);
                        var diskPath = Path.Combine(vmDriveFolder, $"{vmName}-openebs.vhdx");

                        node.Status = "openebs: checking";

                        if (hyperv.ListVmDrives(vmName).Count() < 2)
                        {
                            // The cStor disk doesn't already exist.

                            node.Status = "openebs: stop VM";
                            hyperv.StopVm(vmName);

                            node.Status = "openebs: add cStor disk";
                            hyperv.AddVmDrive(vmName,
                                new VirtualDrive()
                                {
                                    Path      = diskPath,
                                    Size      = diskSize,
                                    IsDynamic = false
                                });

                            node.Status = "openebs: restart VM";
                            hyperv.StartVm(vmName);
                        }
                    }
                },
                (controller, node) => node.Metadata.OpenEbsStorage);
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

        /// <inheritdoc/>
        public override IEnumerable<string> GetClusterAddresses()
        {
            if (cluster.SetupDetails.PublicAddresses?.Any() ?? false)
            {
                return cluster.SetupDetails.PublicAddresses;
            }

            return cluster.SetupDetails.ClusterDefinition.ControlNodes.Select(controlPlane => controlPlane.Address);
        }

        /// <inheritdoc/>
        public override async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reservedMemory = 0, long reserveDisk = 0)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(reservedMemory >= 0, nameof(reservedMemory));
            Covenant.Requires<ArgumentNullException>(reserveDisk >= 0, nameof(reserveDisk));

            var hostMachineName = Environment.MachineName;
            var allNodeNames    = cluster.SetupDetails.ClusterDefinition.NodeDefinitions.Keys.ToList();
            var deploymentCheck = new HostingResourceAvailability();

            // Verify that no VMs are already running that will conflict with VMs
            // that we'd be creating for the cluster.

            var clusterVmNames = new Dictionary<string, NodeDefinition>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in cluster.SetupDetails.ClusterDefinition.Nodes)
            {
                clusterVmNames.Add(GetVmName(node), node);
            }

            using (var hyperv = new HyperVProxy())
            {
                foreach (var vm in hyperv.ListVms())
                {
                    if (!deploymentCheck.Constraints.TryGetValue(hostMachineName, out var hostContraintList))
                    {
                        hostContraintList = new List<HostingResourceConstraint>();

                        deploymentCheck.Constraints.Add(hostMachineName, hostContraintList);
                    }

                    if (clusterVmNames.TryGetValue(vm.Name, out var conflictNode))
                    {
                        hostContraintList.Add(
                            new HostingResourceConstraint()
                            {
                                ResourceType = HostingConstrainedResourceType.VmHost,
                                Nodes        = new List<string>() { conflictNode.Name },
                                Details      = $"Cannot deploy the VM [{conflictNode.Name}] cluster node because the [{vm.Name}] virtual machine already exists."
                            });
                    }
                }
            }

            // We're going to allow CPUs to be oversubscribed but not RAM or disk.
            // Hyper-V does have some limits on the number of virtual machines that
            // can be deployed but that number is in the 100s, so we're not going
            // to worry about that.
            //
            // We will honor the memory and disk reservations for Hyper-V.

            //-----------------------------------------------------------------
            // Check disk capacity:

            // Total the disk space required for all of the cluster nodes.

            var requiredDisk = 0L;

            foreach (var node in cluster.SetupDetails.ClusterDefinition.NodeDefinitions.Values)
            {
                requiredDisk += node.Vm.GetOsDisk(cluster.SetupDetails.ClusterDefinition);

                if (node.OpenEbsStorage)
                {
                    switch (cluster.SetupDetails.ClusterDefinition.Storage.OpenEbs.Engine)
                    {
                        case OpenEbsEngine.cStor:
                        case OpenEbsEngine.Mayastor:

                            requiredDisk += node.Vm.GetOpenEbsDiskSizeBytes(cluster.SetupDetails.ClusterDefinition);
                            break;

                        default:

                            break;  // The other engines don't provision an extra drive.
                    }
                }
            }

            // Determine the free disk space on the drive where the cluster node
            // VHDX files will be deployed.

            var diskLocation = cluster.SetupDetails.ClusterDefinition.Hosting.Vm.DiskLocation;

            if (string.IsNullOrEmpty(diskLocation))
            {
                // $hack(jefflill):
                //
                // neon-desktop installs node VHDX within the [%USERPROFILE%\.neonkube\Desktop] directory
                // by default and this should be on the same drive where Hyper-V deploys disk images by
                // default as well, so we'll check disk constraints on this drive by default.

                diskLocation = KubeHelper.DesktopFolder;
            }

            var availableDisk = new DriveInfo(diskLocation).AvailableFreeSpace;

            // Verify that we have enough disk, taking the reservation into account.

            if (availableDisk - reserveDisk < requiredDisk)
            {
                if (!deploymentCheck.Constraints.TryGetValue(hostMachineName, out var hostContraintList))
                {
                    hostContraintList = new List<HostingResourceConstraint>();

                    deploymentCheck.Constraints.Add(hostMachineName, hostContraintList);
                }

                var humanRequiredDisk  = ByteUnits.Humanize(requiredDisk, powerOfTwo: true);
                var humanReservedDisk  = ByteUnits.Humanize(reserveDisk, powerOfTwo: true);
                var humanAvailableDisk = ByteUnits.Humanize(availableDisk, powerOfTwo: true);

                hostContraintList.Add(
                    new HostingResourceConstraint()
                    {
                         ResourceType = HostingConstrainedResourceType.Disk,
                         Nodes        = allNodeNames,
                         Details      = $"[{humanRequiredDisk}] disk is required but only [{humanAvailableDisk}] is available after reserving [{humanReservedDisk}]."
                    });
            }

            //-----------------------------------------------------------------
            // Check memory capacity:

            // Total the physical memory required for all of the cluster nodes.

            var requiredMemory = 0L;

            foreach (var node in cluster.SetupDetails.ClusterDefinition.NodeDefinitions.Values)
            {
                var vmMemory = node.Vm.Memory;

                if (string.IsNullOrEmpty(vmMemory))
                {
                    vmMemory = cluster.SetupDetails.ClusterDefinition.Hosting.Vm.Memory;
                }

                requiredMemory += (long)ByteUnits.Parse(vmMemory);
            }

            // Determine the free physical memory available on the current machine.

            var memoryStatus = new MEMORYSTATUSEX();

            if (!Win32.GlobalMemoryStatusEx(memoryStatus))
            {
                var error = Marshal.GetLastWin32Error();

                if (!deploymentCheck.Constraints.TryGetValue(hostMachineName, out var hostContraintList))
                {
                    hostContraintList = new List<HostingResourceConstraint>();

                    deploymentCheck.Constraints.Add(hostMachineName, hostContraintList);
                }

                hostContraintList.Add(
                    new HostingResourceConstraint()
                    {
                        ResourceType = HostingConstrainedResourceType.Memory,
                        Nodes        = allNodeNames,
                        Details      = "Windows memory details are not available."
                    });
            }
            else
            {
                // Verify that we have enough memory, taking reserved memory into account.

                var physicalMemory  = (long)memoryStatus.ullTotalPhys;
                var availableMemory = physicalMemory - reservedMemory;

                if (availableMemory < requiredMemory)
                {
                    if (!deploymentCheck.Constraints.TryGetValue(hostMachineName, out var hostContraintList))
                    {
                        hostContraintList = new List<HostingResourceConstraint>();

                        deploymentCheck.Constraints.Add(hostMachineName, hostContraintList);
                    }

                    var humanPhysicalMemory  = ByteUnits.Humanize(physicalMemory,  powerOfTwo: true);
                    var humanAvailableMemory = ByteUnits.Humanize(physicalMemory, powerOfTwo: true);
                    var humanRequiredMemory  = ByteUnits.Humanize(requiredMemory, powerOfTwo: true);
                    var humanReservedMemory  = ByteUnits.Humanize(reservedMemory, powerOfTwo: true);

                    hostContraintList.Add(
                        new HostingResourceConstraint()
                        {
                             ResourceType = HostingConstrainedResourceType.Memory,
                             Nodes        = allNodeNames,
                             Details      = $"[{humanRequiredMemory}] physical memory is required but only [{humanAvailableMemory}] out of [{humanPhysicalMemory}] is available after reserving [{humanReservedMemory}] for the system and other apps."
                        });
                }
            }

            return deploymentCheck;
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine that will host the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            return $"{cluster.SetupDetails.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupDetails.ClusterDefinition)}{node.Name}";
        }

        /// <summary>
        /// Converts a virtual machine name to the matching node definition.
        /// </summary>
        /// <param name="vmName">The virtual machine name.</param>
        /// <returns>The matching node definition or <c>null</c>.</returns>
        private NodeDefinition VmNameToNodeDefinition(string vmName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(vmName), nameof(vmName));

            // Special case the built-in neon-desktop cluster.

            if (cluster.SetupDetails.ClusterDefinition.IsDesktop && 
                vmName.Equals(KubeConst.NeonDesktopHyperVBuiltInVmName, StringComparison.InvariantCultureIgnoreCase) &&
                cluster.SetupDetails.ClusterDefinition.NodeDefinitions.TryGetValue(vmName, out var nodeDefinition))
            {
                return nodeDefinition;
            }

            var prefix = cluster.SetupDetails.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupDetails.ClusterDefinition);

            if (!vmName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var nodeName = vmName.Substring(prefix.Length);

            if (cluster.SetupDetails.ClusterDefinition.NodeDefinitions.TryGetValue(nodeName, out nodeDefinition))
            {
                return nodeDefinition;
            }

            return null;
        }

        /// <summary>
        /// Performs any required Hyper-V initialization before cluster nodes can be provisioned.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task PrepareHyperVAsync(SetupController<NodeDefinition> controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            // Handle any necessary Hyper-V initialization.

            using (var hyperv = new HyperVProxy())
            {
                // Manage the Hyper-V virtual switch.  This will be an internal switch
                // when [UseInternalSwitch=TRUE] otherwise this will be external.

                if (hostingOptions.UseInternalSwitch)
                {
                    switchName = KubeConst.HyperVInternalSwitchName;

                    controller.SetGlobalStepStatus($"configure: [{switchName}] switch");

                    // We're going to create an internal switch named [neonkube] configured
                    // with the standard private subnet and a NAT to enable external routing.

                    var @switch = hyperv.FindSwitch(switchName);

                    if (@switch == null)
                    {
                        // The internal switch doesn't exist yet, so create it.  Note that
                        // this switch requires a virtual NAT.

                        controller.SetGlobalStepStatus($"add: [{switchName}] switch with NAT for [{hostingOptions.NeonKubeInternalSubnet}]");
                        hyperv.NewInternalSwitch(switchName, hostingOptions.NeonKubeInternalSubnet, addNat: true);
                        controller.SetGlobalStepStatus();
                    }

                    controller.SetGlobalStepStatus();
                }
                else
                {
                    // We're going to create an external Hyper-V switch if there
                    // isn't already an external switch.

                    controller.SetGlobalStepStatus("scan: network adapters");

                    var externalSwitch = hyperv.ListSwitches().FirstOrDefault(@switch => @switch.Type == VirtualSwitchType.External);

                    if (externalSwitch == null)
                    {
                        hyperv.NewExternalSwitch(switchName = defaultSwitchName, NetHelper.ParseIPv4Address(cluster.SetupDetails.ClusterDefinition.Network.Gateway));
                    }
                    else
                    {
                        switchName = externalSwitch.Name;
                    }
                }

                controller.ThrowIfCancelled();

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                controller.SetGlobalStepStatus("scan: virtual machines");

                var existingMachines = hyperv.ListVms();
                var conflicts        = string.Empty;
                var conflictCount    = 0;

                controller.SetGlobalStepStatus("stop: virtual machines");

                foreach (var machine in existingMachines)
                {
                    var nodeDefinition = VmNameToNodeDefinition(machine.Name);

                    if (nodeDefinition == null)
                    {
                        continue;
                    }

                    var nodeName    = nodeDefinition.Name;
                    var drivePath   = Path.Combine(vmDriveFolder, $"{machine.Name}.vhdx");
                    var isClusterVM = cluster.FindNode(nodeName) != null;

                    // We're going to report errors when one or more cluster VMs already exist.

                    if (conflicts.Length > 0)
                    {
                        conflicts += ", ";
                    }

                    conflicts += nodeName;
                }

                if (conflictCount == 1)
                {
                    throw new HyperVException($"[{conflicts}] virtual machine already exists.");
                }
                else if (conflictCount > 1)
                {
                    throw new HyperVException($"[{conflicts}] virtual machines already exist.");
                }

                controller.SetGlobalStepStatus();
            }
        }

        /// <summary>
        /// Creates a Hyper-V virtual machine for a cluster node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The target node.</param>
        private void ProvisionVM(SetupController<NodeDefinition> controller, NodeSshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            using (var hyperv = new HyperVProxy())
            {
                var vmName = GetVmName(node.Metadata);

                // Decompress the VHDX template file to the virtual machine's
                // virtual hard drive file.

                var driveTemplateInfoPath = driveTemplatePath + ".info";
                var osDrivePath           = Path.Combine(vmDriveFolder, $"{vmName}.vhdx");

                using (var input = new FileStream(driveTemplatePath, FileMode.Open, FileAccess.Read))
                {
                    // Delete any existing OS VHDX file for the node.  Note that this will fail
                    // when the file happens to be used by a running VM that doesn't follow our
                    // VM naming conventions.
                    //
                    // We're doing this for robustness to handle the situation where an OS VHDX
                    // was decompressed during an earlier cluster provisioning operation but that
                    // operation was interruped before the VM was provisioned or the VHDX wasn't
                    // removed after the VM was deleted.

                    NeonHelper.DeleteFile(osDrivePath);

                    // Decompress the VHDX.

                    using (var output = new FileStream(osDrivePath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (var decompressor = new GZipStream(input, CompressionMode.Decompress))
                        {
                            var     buffer = new byte[64 * 1024];
                            long    cbRead = 0;
                            int     cb;

                            while (true)
                            {
                                controller.ThrowIfCancelled();

                                cb     = decompressor.Read(buffer, 0, buffer.Length);
                                cbRead = input.Position;

                                if (cb == 0)
                                {
                                    break;
                                }

                                output.Write(buffer, 0, cb);

                                var percentComplete = (int)((double)cbRead / (double)input.Length * 100.0);

                                controller.SetGlobalStepStatus($"decompress: node VHDX [{percentComplete}%]");
                            }

                            controller.SetGlobalStepStatus($"decompress: node VHDX [100%]");
                        }
                    }

                    controller.SetGlobalStepStatus();
                }

                // Create the virtual machine.

                var processors  = node.Metadata.Vm.GetCores(cluster.SetupDetails.ClusterDefinition);
                var memoryBytes = node.Metadata.Vm.GetMemory(cluster.SetupDetails.ClusterDefinition);
                var osDiskBytes = node.Metadata.Vm.GetOsDisk(cluster.SetupDetails.ClusterDefinition);

                node.Status = $"create: virtual machine";
                hyperv.AddVm(
                    vmName,
                    processorCount: processors,
                    driveSize:      osDiskBytes.ToString(),
                    memorySize:     memoryBytes.ToString(),
                    drivePath:      osDrivePath,
                    switchName:     switchName);

                // Create a temporary ISO with the [neon-init.sh] script, mount it
                // to the VM and then boot the VM for the first time.  The script on
                // the ISO will be executed automatically by the [neon-init] service
                // preinstalled on the VM image and the script will configure the
                // secure SSH password and then the network.
                //
                // This ensures that SSH is not exposed to the network before the
                // secure password has been set.

                var tempIso = (TempFile)null;

                try
                {
                    // Create a temporary ISO with the prep script and mount it
                    // to the node VM.

                    controller.ThrowIfCancelled();

                    node.Status = $"mount: neon-init iso";
                    tempIso     = KubeHelper.CreateNeonInitIso(node.Cluster.SetupDetails.ClusterDefinition, node.Metadata, nodeMtu: NodeMtu, newPassword: secureSshPassword);

                    hyperv.InsertVmDvd(vmName, tempIso.Path);

                    // Start the VM for the first time with the mounted ISO.  The network
                    // configuration will happen automatically by the time we can connect.

                    controller.ThrowIfCancelled();

                    node.Status = $"start: virtual machine";
                    hyperv.StartVm(vmName);

                    // Update the node credentials to use the secure password for normal clusters or the
                    // hardcoded SSH key for ready-to-go neon-desktop clusters and then wait for the node
                    // to boot.

                    controller.ThrowIfCancelled();

                    if (controller.Get<bool>(KubeSetupProperty.DesktopReadyToGo))
                    {
                        node.UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, KubeHelper.GetBuiltinDesktopSshKey().PrivatePEM));
                    }
                    else
                    {
                        node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, secureSshPassword));
                    }

                    node.WaitForBoot();
                    controller.ThrowIfCancelled();

                    // Extend the primary partition and file system to fill 
                    // the virtual drive.
                    //
                    // Note that there should only be one partitioned disk at
                    // this point: the OS disk.

                    var partitionedDisks = node.ListPartitionedDisks();
                    var osDisk           = partitionedDisks.Single();

                    node.Status = $"resize: OS disk";

                    var response = node.SudoCommand($"growpart {osDisk} 2", RunOptions.None);

                    // Ignore errors reported when the partition is already at its
                    // maximum size and cannot be grown:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1352

                    if (!response.Success && !response.AllText.Contains("NOCHANGE:"))
                    {
                        response.EnsureSuccess();
                    }

                    node.SudoCommand($"resize2fs {osDisk}2", RunOptions.FaultOnError);
                }
                finally
                {
                    // Be sure to delete the ISO file so these don't accumulate.

                    tempIso?.Dispose();
                }
            }
        }

        //---------------------------------------------------------------------
        // Cluster life-cycle methods

        /// <inheritdoc/>
        public override HostingCapabilities Capabilities => HostingCapabilities.Stoppable | HostingCapabilities.Pausable | HostingCapabilities.Removable;

        /// <inheritdoc/>
        public override async Task<ClusterHealth> GetClusterHealthAsync(TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            using (var hyperV = new HyperVProxy())
            {
                if (timeout <= TimeSpan.Zero)
                {
                    timeout = DefaultStatusTimeout;
                }

                // We're going to infer the cluster provisiong status by examining the
                // cluster login and the state of the VMs deployed in the local Hyper-V.

                var contextName = $"root@{cluster.SetupDetails.ClusterDefinition.Name}";
                var context     = KubeHelper.Config.GetContext(contextName);

                // Create a hashset with the names of the nodes that map to deployed Hyper-V
                // virtual machines.  Wre're also going to create a dictionary mapping the
                // existing virtual machine names to the machine information.

                var existingNodes    = new HashSet<string>();
                var existingMachines = new Dictionary<string, VirtualMachine>(StringComparer.InvariantCultureIgnoreCase);

                foreach (var machine in hyperV.ListVms())
                {
                    var nodeDefinition = VmNameToNodeDefinition(machine.Name);

                    if (nodeDefinition != null)
                    {
                        existingNodes.Add(nodeDefinition.Name);
                    }

                    existingMachines.Add(machine.Name, machine);
                }

                // Build the cluster status.

                if (context == null)
                {
                    // The Kubernetes context for this cluster doesn't exist, so we know that any
                    // virtual machines with names matching the virtual machines that would be
                    // provisioned for the cluster definition are conflicting.

                    var clusterHealth = new ClusterHealth()
                    {
                        State   = ClusterState.NotFound,
                        Summary = "Cluster does not exist"
                    };

                    foreach (var node in cluster.SetupDetails.ClusterDefinition.NodeDefinitions.Values)
                    {
                        clusterHealth.Nodes.Add(node.Name, existingNodes.Contains(node.Name) ? ClusterNodeState.Conflict : ClusterNodeState.NotProvisioned);
                    }

                    return clusterHealth;
                }
                else
                {
                    // We're going to assume that all virtual machines that match cluster node names
                    // (after stripping off any cluster prefix) belong to the cluster and we'll map
                    // the actual VM states to public node states.

                    var clusterHealth = new ClusterHealth();

                    foreach (var node in cluster.SetupDetails.ClusterDefinition.NodeDefinitions.Values)
                    {
                        var nodeState = ClusterNodeState.NotProvisioned;

                        if (existingNodes.Contains(node.Name))
                        {
                            var vmName  = GetVmName(node);

                            if (existingMachines.TryGetValue(vmName, out var machine))
                            {
                                switch (machine.State)
                                {
                                    case VirtualMachineState.Unknown:

                                        nodeState = ClusterNodeState.Unknown;
                                        break;

                                    case VirtualMachineState.Off:

                                        nodeState = ClusterNodeState.Off;
                                        break;

                                    case VirtualMachineState.Starting:

                                        nodeState = ClusterNodeState.Starting;
                                        break;

                                    case VirtualMachineState.Running:

                                        nodeState = ClusterNodeState.Running;
                                        break;

                                    case VirtualMachineState.Paused:

                                        nodeState = ClusterNodeState.Unknown;
                                        break;

                                    case VirtualMachineState.Saved:

                                        nodeState = ClusterNodeState.Paused;
                                        break;

                                    default:

                                        throw new NotImplementedException();
                                }
                            }
                        }

                        clusterHealth.Nodes.Add(node.Name, nodeState);
                    }

                    // We're going to examine the node states from the Hyper-V perspective and
                    // short-circuit the Kubernetes level cluster health check when the cluster
                    // nodes are not provisioned, are paused or appear to be transitioning
                    // between starting, stopping, waking, or paused states.

                    var commonNodeState = clusterHealth.Nodes.Values.First();

                    foreach (var nodeState in clusterHealth.Nodes.Values)
                    {
                        if (nodeState != commonNodeState)
                        {
                            // Nodes have differing states so we're going to consider the cluster
                            // to be transitioning.

                            clusterHealth.State   = ClusterState.Transitioning;
                            clusterHealth.Summary = "Cluster is transitioning";
                            break;
                        }
                    }

                    if (cluster.SetupDetails.DeploymentStatus != ClusterDeploymentStatus.Ready)
                    {
                        clusterHealth.State   = ClusterState.Configuring;
                        clusterHealth.Summary = "Cluster is partially configured";
                    }
                    else if (clusterHealth.State != ClusterState.Transitioning)
                    {
                        // If we get here then all of the nodes have the same state so
                        // we'll use that common state to set the overall cluster state.

                        switch (commonNodeState)
                        {
                            case ClusterNodeState.Paused:

                                clusterHealth.State    = ClusterState.Paused;
                                clusterHealth.Summary = "Cluster is paused";
                                break;

                            case ClusterNodeState.Starting:

                                clusterHealth.State   = ClusterState.Unhealthy;
                                clusterHealth.Summary = "Cluster is starting";
                                break;

                            case ClusterNodeState.Running:

                                clusterHealth.State   = ClusterState.Healthy;
                                clusterHealth.Summary = "Cluster is configured";
                                break;

                            case ClusterNodeState.Off:

                                clusterHealth.State   = ClusterState.Off;
                                clusterHealth.Summary = "Cluster is turned off";
                                break;

                            case ClusterNodeState.NotProvisioned:

                                clusterHealth.State   = ClusterState.NotFound;
                                clusterHealth.Summary = "Cluster is not found.";
                                break;

                            case ClusterNodeState.Unknown:
                            default:

                                clusterHealth.State   = ClusterState.Unknown;
                                clusterHealth.Summary = "Cluster not found";
                                break;
                        }
                    }

                    if (clusterHealth.State == ClusterState.Off)
                    {
                        clusterHealth.Summary = "Cluster is turned off";

                        return clusterHealth;
                    }

                    return clusterHealth;
                }
            }
        }

        /// <inheritdoc/>
        public override async Task StartClusterAsync()
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            // We just need to start any cluster VMs that aren't already running.

            using (var hyperv = new HyperVProxy())
            {
                Parallel.ForEach(cluster.SetupDetails.ClusterDefinition.Nodes, parallelOptions,
                    node =>
                    {
                        var vmName = GetVmName(node);
                        var vm     = hyperv.FindVm(vmName);

                        if (vm == null)
                        {
                            // We may see this when the cluster definition doesn't match the 
                            // deployed cluster VMs.  We're just going to ignore this situation.

                            return;
                        }

                        switch (vm.State)
                        {
                            case VirtualMachineState.Off:
                            case VirtualMachineState.Saved:

                                hyperv.StartVm(vmName);
                                break;

                            case VirtualMachineState.Running:
                            case VirtualMachineState.Starting:

                                break;

                            default:
                            case VirtualMachineState.Paused:
                            case VirtualMachineState.Unknown:

                                throw new NotImplementedException($"Unexpected VM state: {vmName}:{vm.State}");
                        }
                    });
            }
        }

        /// <inheritdoc/>
        public override async Task StopClusterAsync(StopMode stopMode = StopMode.Graceful)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            // We just need to stop any running cluster VMs.

            using (var hyperv = new HyperVProxy())
            {
                Parallel.ForEach(cluster.SetupDetails.ClusterDefinition.Nodes, parallelOptions,
                    node =>
                    {
                        var vmName = GetVmName(node);
                        var vm     = hyperv.FindVm(vmName);

                        if (vm == null)
                        {
                            // We may see this when the cluster definition doesn't match the 
                            // deployed cluster VMs.  We're just going to ignore this.

                            return;
                        }

                        switch (vm.State)
                        {
                            case VirtualMachineState.Off:
                            case VirtualMachineState.Saved:

                                break;

                            case VirtualMachineState.Paused:
                            case VirtualMachineState.Running: 
                            case VirtualMachineState.Starting:

                                switch (stopMode)
                                {
                                    case StopMode.Pause:

                                        hyperv.SaveVm(vmName);
                                        break;

                                    case StopMode.Graceful:

                                        hyperv.StopVm(vmName);
                                        break;

                                    case StopMode.TurnOff:

                                        hyperv.StopVm(vmName, turnOff: true);
                                        break;
                                }
                                break;

                            default:
                            case VirtualMachineState.Unknown:

                                throw new NotImplementedException($"Unexpected VM state: {vmName}:{vm.State}");
                        }
                    });
            }
        }

        /// <inheritdoc/>
        public override async Task DeleteClusterAsync(bool removeOrphans = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            using (var hyperv = new HyperVProxy())
            {
                // If [removeOrphans=true] and the cluster definition specifies a
                // VM name prefix, then we'll simply remove all VMs with that prefix.
                // Otherwise, we'll do a normal remove.

                var vmPrefix = cluster.SetupDetails.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupDetails.ClusterDefinition);

                if (removeOrphans && !string.IsNullOrEmpty(vmPrefix))
                {
                    Parallel.ForEach(hyperv.ListVms().Where(vm => vm.Name.StartsWith(vmPrefix)), parallelOptions,
                        vm =>
                        {
                            if (vm.State == VirtualMachineState.Running || vm.State == VirtualMachineState.Starting)
                            {
                                hyperv.StopVm(vm.Name, turnOff: true);
                            }

                            hyperv.RemoveVm(vm.Name);
                        });

                    return;
                }

                // All we need to do for Hyper-V clusters is turn off and remove the cluster VMs.
                // Note that we're just turning nodes off to save time and because we're going
                // to be deleting them all anyway.
                //
                // We're going to leave any virtual switches alone.

                await StopClusterAsync(stopMode: StopMode.TurnOff);

                // Remove all of the cluster VMs.

                Parallel.ForEach(cluster.SetupDetails.ClusterDefinition.Nodes, parallelOptions,
                    node =>
                    {
                        var vmName = GetVmName(node);
                        var vm     = hyperv.FindVm(vmName);

                        if (vm == null)
                        {
                            // We may see this when the cluster definition doesn't match the 
                            // deployed cluster VMs or when the cluster doesn't exist or when
                            // the cluster deployment is in progress and this node VM hasn't.
                            // been created yet.
                            //
                            // It's possible that the VHDX files could exist though, so we'll
                            // go ahead and delete them, if present.

                            NeonHelper.DeleteFile(Path.Combine(vmDriveFolder, $"{vmName}.vhdx"));
                            NeonHelper.DeleteFile(Path.Combine(vmDriveFolder, $"{vmName}-openebs.vhdx"));
                            return;
                        }

                        hyperv.RemoveVm(vmName);
                    });

                // Remove any potentially orphaned VMs when enabled and a prefix is specified.

                if (removeOrphans && !string.IsNullOrEmpty(cluster.SetupDetails.ClusterDefinition.Deployment.Prefix))
                {
                    var prefix = cluster.SetupDetails.ClusterDefinition.Deployment.Prefix + "-";

                    Parallel.ForEach(hyperv.ListVms(), parallelOptions,
                        vm =>
                        {
                            if (vm.Name.StartsWith(prefix))
                            {
                                hyperv.RemoveVm(vm.Name);
                            }
                        });
                }
            }
        }
    }
}
