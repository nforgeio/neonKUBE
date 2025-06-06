//-----------------------------------------------------------------------------
// FILE:        HyperVHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Kube.Deployment;
using Neon.HyperV;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Kube.SSH;
using Neon.Net;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;
using Neon.Windows;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using YamlDotNet.Serialization;

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
        // Private types

        /// <summary>
        /// Maps a Hyper-V virtual machine to the corresponding cluster node name.
        /// </summary>
        private struct ClusterVm
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="machine">Specifies the virtual machine.</param>
            /// <param name="nodeName">Specifies the associated cluster node name.</param>
            public ClusterVm(VirtualMachine machine, string nodeName)
            {
                Covenant.Requires<ArgumentNullException>(machine != null, nameof(machine));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));

                this.Machine  = machine;
                this.NodeName = nodeName;
            }

            /// <summary>
            /// Returns the Hyper-V virtual machine.
            /// </summary>
            public VirtualMachine Machine { get; private set; }

            /// <summary>
            /// Returns the associated cluster node name.
            /// </summary>
            public string NodeName { get; private set; }
        }

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
            // This method can't do nothing because the C# compiler may optimize calls
            // out of trimmed executables and we need this type to be discoverable
            // via reflection.
            //
            // This call does almost nothing to prevent C# code optimization.

            Load(() => new HyperVHostingManager());
        }

        //---------------------------------------------------------------------
        // Instance members.

        private const string defaultSwitchName = "external";
        private const string tagMarkerLine     = "neonkube";
        private const string clusterIdTag      = "neon-cluster-id";
        private const string nodeNameTag       = "neon-node-name";

        private ClusterProxy                        cluster;
        private bool                                debugMode;
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
        /// <b>debug mode</b> is implied when both <paramref name="nodeImageUri"/> and <paramref name="nodeImagePath"/> are <c>null</c> or empty.
        /// </note>
        /// </remarks>
        public HyperVHostingManager(ClusterProxy cluster, bool cloudMarketplace, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager = this;

            this.cluster        = cluster;
            this.debugMode      = string.IsNullOrEmpty(nodeImageUri) && string.IsNullOrEmpty(nodeImagePath);
            this.nodeImageUri   = nodeImageUri;
            this.nodeImagePath  = nodeImagePath;
            this.hostingOptions = cluster.Hosting.HyperV;

            // Determine where we're going to place the VM hard drive files and
            // ensure that the directory exists.

            if (cluster.Hosting.Hypervisor != null && !string.IsNullOrEmpty(cluster.Hosting.Hypervisor.DiskLocation))
            {
                vmDriveFolder = cluster.Hosting.Hypervisor.DiskLocation;
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
            Covenant.Assert(clusterDefinition.Hosting.Environment == HostingEnvironment.HyperV, $"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.HyperV}].");

            if (clusterDefinition.Hosting.Environment != HostingEnvironment.HyperV)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.HyperV}].");
            }
        }

        /// <inheritdoc/>
        public override async Task CheckDeploymentReadinessAsync(ClusterDefinition clusterDefinition)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var readiness = new HostingReadiness();

            // Collect information about the cluster nodes so we can verify that
            // cluster makes sense.

            var hostedNodes = clusterDefinition.Nodes
                .Select(nodeDefinition => new HostedNodeInfo(nodeDefinition.Name, nodeDefinition.Role, nodeDefinition.Hypervisor.GetVCpus(clusterDefinition), nodeDefinition.Hypervisor.GetMemory(clusterDefinition)))
                .ToList();

            ValidateCluster(clusterDefinition, hostedNodes, readiness);

            // Verify that Hyper-V is available.

            try
            {
                using (var hyperv = new HyperVProxy())
                {
                    hyperv.ListVms();
                }
            }
            catch (Exception e)
            {
                readiness.AddProblem(type: HostingReadinessProblem.HyperVType, $"Hyper-V is not available locally: {NeonHelper.ExceptionError(e)}");
            }

            readiness.ThrowIfNotReady();
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            this.controller = controller;

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.SetupState.ClusterDefinition);

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in cluster.SetupState.ClusterDefinition.Nodes)
            {
                node.Labels.PhysicalMachine     = Environment.MachineName;
                node.Labels.StorageBootDiskSize = ByteUnits.ToGiB(node.Hypervisor.GetMemory(cluster.SetupState.ClusterDefinition));
            }

            // Add the provisioning steps to the controller.

            controller.MaxParallel = 1; // We're only going to provision one VM at a time on the local Hyper-V.

            controller.AddGlobalStep("check hyper-v",
                controller =>
                {
                    this.secureSshPassword = cluster.SetupState.SshPassword;

                    // If the cluster is being deployed to the internal [neonkube] switch, we need to
                    // check to see whether the switch already exists, and if it does, we'll need to
                    // ensure that it's configured correctly with a virtual address and NAT.  We're
                    // going to fail setup when an existing switch isn't configured correctly.

                    if (cluster.Hosting.HyperV.UseInternalSwitch)
                    {
                        using (var hyperv = new HyperVProxy())
                        {
                            controller.SetGlobalStepStatus($"check: [{KubeConst.HyperVInternalSwitchName}] virtual switch/NAT");

                            var localHyperVOptions = cluster.Hosting.HyperV;
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

            // Download and start the base image for debug mode, otherwise download and
            // start the node image.

            if (debugMode)
            {
                controller.AddGlobalStep($"hyper-v node image",
                    async state =>
                    {
                        // Download the GZIPed base image VHDX template if it's not already present and
                        // has a valid MD5 hash file.
                        //
                        // Note that we're going to name the file the same as the file name from the URI.

                        string driveTemplateName;

                        nodeImageUri = await KubeDownloads.GetBaseImageUri(HostingEnvironment.HyperV);

                        var driveTemplateUri = new Uri(nodeImageUri);

                        driveTemplateName = Path.GetFileNameWithoutExtension(driveTemplateUri.Segments.Last());
                        driveTemplatePath = Path.Combine(KubeHelper.VmImageFolder, driveTemplateName);

                        await KubeHelper.DownloadNodeImageAsync(nodeImageUri, driveTemplatePath,
                            (progressType, progress) =>
                            {
                                controller.SetGlobalStepStatus($"{NeonHelper.EnumToString(progressType)}: VHDX [{progress}%] [{driveTemplateName}]");

                                return !controller.IsCancelPending;
                            },
                            strictCheck: false);
                    });
            }
            else
            {
                if (!controller.Get<bool>(KubeSetupProperty.DisableImageDownload, false))
                {
                    controller.AddGlobalStep($"hyper-v node image",
                        async state =>
                        {
                            // Download the GZIPed VHDX node image template if it's not already present and has a valid
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
                                    },
                                    strictCheck: false);
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
                    Covenant.Assert(File.Exists(nodeImagePath), () => $"Missing file: {nodeImagePath}");

                    driveTemplatePath = nodeImagePath;
                }
            }

            var typedController = (SetupController<NodeDefinition>)controller;
            var createVmLabel   = "create virtual machine";

            if (cluster.SetupState.ClusterDefinition.Nodes.Count() > 1)
            {
                createVmLabel += "(s)";
            }

            controller.AddGlobalStep("configure hyper-v", async controller => await PrepareHyperVAsync(typedController));
            controller.AddNodeStep(createVmLabel, (controller, node) => ProvisionVM(typedController, node));
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition = cluster.SetupState.ClusterDefinition;

            if (clusterDefinition.Storage.OpenEbs.Mayastor)
            {
                // We need to add any required OpenEBS Mayastor disk after the node has been otherwise
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
                            var diskSize = ByteUnits.Parse(clusterDefinition.Hosting.Hypervisor.MayastorDiskSize);
                            var diskPath = Path.Combine(vmDriveFolder, $"{vmName}-openebs.vhdx");

                            node.Status = "openebs: checking";

                            if (hyperv.ListVmDrives(vmName).Count() < 2)
                            {
                                // The Mayastor disk doesn't already exist.

                                node.Status = "openebs: stop VM";
                                hyperv.StopVm(vmName);

                                node.Status = "openebs: create data disk";
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
        public override async Task<string> CheckForConflictsAsync(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            return await CheckForIPConflictsAsync(clusterDefinition);
        }

        /// <inheritdoc/>
        public override IEnumerable<string> GetClusterAddresses()
        {
            if (cluster.SetupState.PublicAddresses?.Any() ?? false)
            {
                return cluster.SetupState.PublicAddresses;
            }

            return cluster.SetupState.ClusterDefinition.ControlNodes.Select(controlPlane => controlPlane.Address);
        }

        /// <inheritdoc/>
        public override async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reservedMemory = 0, long reservedDisk = 0)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(reservedMemory >= 0, nameof(reservedMemory));
            Covenant.Requires<ArgumentNullException>(reservedDisk >= 0, nameof(reservedDisk));

            if (reservedMemory == 0)
            {
                reservedMemory = (long)ByteUnits.Parse("500 MiB");
            }

            var hostMachineName   = Environment.MachineName;
            var clusterDefinition = cluster.SetupState.ClusterDefinition;
            var allNodeNames      = clusterDefinition.NodeDefinitions.Keys.ToList();
            var deploymentCheck   = new HostingResourceAvailability();

            // Verify that no VMs are already running that will conflict with VMs
            // that we'd be creating for the cluster.

            var clusterVmNames = new Dictionary<string, NodeDefinition>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var node in clusterDefinition.Nodes)
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

            foreach (var node in clusterDefinition.NodeDefinitions.Values)
            {
                requiredDisk += node.Hypervisor.GetBootDiskSizeBytes(clusterDefinition);

                if (node.OpenEbsStorage && clusterDefinition.Storage.OpenEbs.Mayastor)
                {
                    requiredDisk += (long)ByteUnits.Parse(clusterDefinition.Hosting.Hypervisor.MayastorDiskSize);
                }
            }

            // Determine the free disk space on the drive where the cluster node
            // VHDX files will be deployed.

            var diskLocation = cluster.Hosting.Hypervisor.DiskLocation;

            if (string.IsNullOrEmpty(diskLocation))
            {
                // $hack(jefflill):
                //
                // NeonDESKTOP installs node VHDX within the [%USERPROFILE%\.neonkube\Desktop] directory
                // by default and this should be on the same drive where Hyper-V deploys disk images by
                // default as well, so we'll check disk constraints on this drive by default.

                diskLocation = KubeHelper.DesktopFolder;
            }

            var availableDisk = new DriveInfo(diskLocation).AvailableFreeSpace;

            // Verify that we have enough disk, taking the reservation into account.

            if (availableDisk - reservedDisk < requiredDisk)
            {
                if (!deploymentCheck.Constraints.TryGetValue(hostMachineName, out var hostContraintList))
                {
                    hostContraintList = new List<HostingResourceConstraint>();

                    deploymentCheck.Constraints.Add(hostMachineName, hostContraintList);
                }

                var humanRequiredDisk  = ByteUnits.Humanize(requiredDisk, powerOfTwo: true);
                var humanReservedDisk  = ByteUnits.Humanize(reservedDisk, powerOfTwo: true);
                var humanAllowedDisk   = ByteUnits.Humanize(availableDisk - reservedDisk, powerOfTwo: true);

                hostContraintList.Add(
                    new HostingResourceConstraint()
                    {
                         ResourceType = HostingConstrainedResourceType.Disk,
                         Nodes        = allNodeNames,
                         Details      = $"[{humanRequiredDisk}] disk is required but only [{humanAllowedDisk}] is available after reserving [{humanReservedDisk}]."
                    });
            }

            //-----------------------------------------------------------------
            // Check memory capacity:

            // Total the physical memory required for all of the cluster nodes.

            var requiredMemory = 0L;

            foreach (var node in cluster.SetupState.ClusterDefinition.NodeDefinitions.Values)
            {
                var vmMemory = node.Hypervisor.Memory;

                if (string.IsNullOrEmpty(vmMemory))
                {
                    vmMemory = cluster.Hosting.Hypervisor.Memory;
                }

                requiredMemory += (long)ByteUnits.Parse(vmMemory);
            }

            // Determine the free physical memory available on the current machine.

            var memoryStatus = new MEMORYSTATUSEX();

            if (!Win32.GlobalMemoryStatusEx(memoryStatus))
            {
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
                // Verify that we have enough free physical memory, taking reserved free memory into account.

                var freePhysicalMemory      = (long)memoryStatus.ullAvailPhys;
                var availablePhysicalMemory = freePhysicalMemory - reservedMemory;

                if (availablePhysicalMemory < requiredMemory)
                {
                    if (!deploymentCheck.Constraints.TryGetValue(hostMachineName, out var hostContraintList))
                    {
                        hostContraintList = new List<HostingResourceConstraint>();

                        deploymentCheck.Constraints.Add(hostMachineName, hostContraintList);
                    }

                    var humanPhysicalMemory  = ByteUnits.Humanize(freePhysicalMemory,  powerOfTwo: true);
                    var humanAvailableMemory = ByteUnits.Humanize(freePhysicalMemory - reservedMemory, powerOfTwo: true);
                    var humanRequiredMemory  = ByteUnits.Humanize(requiredMemory, powerOfTwo: true);
                    var humanReservedMemory  = ByteUnits.Humanize(reservedMemory, powerOfTwo: true);

                    hostContraintList.Add(
                        new HostingResourceConstraint()
                        {
                             ResourceType = HostingConstrainedResourceType.Memory,
                             Nodes        = allNodeNames,
                             Details      = $"[{humanRequiredMemory}] physical memory is required but only [{humanAvailableMemory}] out of [{humanPhysicalMemory}] is available after reserving [{humanReservedMemory}] for the host and other apps."
                        });
                }
            }

            return deploymentCheck;
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine that will host the node.
        /// </summary>
        /// <param name="nodeDefinition">The target node definition.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));
            cluster.EnsureSetupMode();

            return $"{cluster.Hosting.Hypervisor.GetVmNamePrefix(cluster.SetupState.ClusterDefinition)}{nodeDefinition.Name}";
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine that will host the node.
        /// </summary>
        /// <param name="nodeDeployment">The target node deployment.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDeployment nodeDeployment)
        {
            Covenant.Requires<ArgumentNullException>(nodeDeployment != null, nameof(nodeDeployment));
            Covenant.Assert(cluster.KubeConfig?.Cluster != null, "Use this method only for already deployed clusters.");

            return $"{cluster.KubeConfig.Cluster.HostingNamePrefix}{nodeDeployment.Name}";
        }

        /// <summary>
        /// Converts a virtual machine name to the matching node definition.
        /// </summary>
        /// <param name="vmName">The virtual machine name.</param>
        /// <returns>
        /// The corresponding node name if found, or <c>null</c> when the node VM
        /// could not be identified.
        /// </returns>
        private NodeDefinition VmNameToNodeDefinition(string vmName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(vmName), nameof(vmName));
            Covenant.Assert(cluster?.SetupState?.ClusterDefinition != null);
            cluster.EnsureSetupMode();

            // Special case the NeonDESKTOP cluster.

            if (cluster.SetupState.ClusterDefinition.IsDesktop &&
                vmName.Equals(KubeConst.NeonDesktopHyperVVmName, StringComparison.InvariantCultureIgnoreCase) &&
                cluster.SetupState.ClusterDefinition.NodeDefinitions.TryGetValue(vmName, out var nodeDefinition))
            {
                return nodeDefinition;
            }

            var prefix = cluster.Hosting.Hypervisor.GetVmNamePrefix(cluster.SetupState.ClusterDefinition);

            if (!vmName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var nodeName = vmName.Substring(prefix.Length);

            if (cluster.SetupState.ClusterDefinition.NodeDefinitions.TryGetValue(nodeName, out nodeDefinition))
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
                        hyperv.NewExternalSwitch(switchName = defaultSwitchName, NetHelper.ParseIPv4Address(cluster.SetupState.ClusterDefinition.Network.Gateway));
                    }
                    else
                    {
                        switchName = externalSwitch.Name;
                    }
                }

                controller.ThrowIfCancelledOrFaulted();

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
                                controller.ThrowIfCancelledOrFaulted();

                                cb     = decompressor.Read(buffer, 0, buffer.Length);
                                cbRead = input.Position;

                                if (cb == 0)
                                {
                                    break;
                                }

                                output.Write(buffer, 0, cb);

                                var percentComplete = (int)((double)cbRead / (double)input.Length * 100.0);

                                node.Status = $"decompress: node VHDX [{percentComplete}%]";
                            }

                            node.Status = $"decompress: node VHDX [100%]";
                        }
                    }

                    controller.SetGlobalStepStatus();
                }

                // Create the virtual machine.

                var sbNotes = new StringBuilder();

                sbNotes.AppendLineLinux(tagMarkerLine);
                sbNotes.AppendLineLinux($"{clusterIdTag}: {cluster.SetupState.ClusterId}");
                sbNotes.AppendLineLinux($"{nodeNameTag}: {node.Name}");
                sbNotes.AppendLineLinux(tagMarkerLine);

                var vcpus         = node.Metadata.Hypervisor.GetVCpus(cluster.SetupState.ClusterDefinition);
                var memoryBytes   = node.Metadata.Hypervisor.GetMemory(cluster.SetupState.ClusterDefinition);
                var bootDiskBytes = node.Metadata.Hypervisor.GetBootDiskSizeBytes(cluster.SetupState.ClusterDefinition);

                node.Status = $"create: virtual machine";
                hyperv.AddVm(
                    vmName,
                    processorCount: vcpus,
                    driveSize:      bootDiskBytes.ToString(),
                    memorySize:     memoryBytes.ToString(),
                    drivePath:      osDrivePath,
                    switchName:     switchName,
                    notes:          sbNotes.ToString());

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

                    controller.ThrowIfCancelledOrFaulted();

                    node.Status = $"mount: neon-init iso";
                    tempIso     = KubeHelper.CreateNeonInitIso(node.Cluster.SetupState.ClusterDefinition, node.Metadata, nodeMtu: NodeMtu, newPassword: secureSshPassword);

                    hyperv.InsertVmDvd(vmName, tempIso.Path);

                    // Start the VM for the first time with the mounted ISO.  The network
                    // configuration will happen automatically by the time we can connect.

                    controller.ThrowIfCancelledOrFaulted();

                    node.Status = $"start: virtual machine";
                    hyperv.StartVm(vmName);

                    // Update the node credentials to use the secure password for normal clusters or the
                    // hardcoded SSH key for ready-to-go NeonDESKTOP clusters and then wait for the node
                    // to boot.

                    controller.ThrowIfCancelledOrFaulted();

                    if (controller.Get<bool>(KubeSetupProperty.DesktopReadyToGo))
                    {
                        node.UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, KubeHelper.GetBuiltinDesktopSshKey().PrivatePEM));
                    }
                    else
                    {
                        node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, secureSshPassword));
                    }

                    node.WaitForBoot();
                    controller.ThrowIfCancelledOrFaulted();

                    // Extend the primary partition and file system to fill 
                    // the virtual drive.
                    //
                    // Note that there should only be one partitioned disk at
                    // this point: the boot disk.

                    var partitionedDisks = node.ListPartitionedDisks();
                    var bootDisk         = partitionedDisks.Single();

                    node.Status = $"resize: boot disk";

                    var response = node.SudoCommand($"growpart {bootDisk} 2", RunOptions.None);

                    // Ignore errors reported when the partition is already at its
                    // maximum size and cannot be grown:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1352

                    if (!response.Success && !response.AllText.Contains("NOCHANGE:"))
                    {
                        response.EnsureSuccess();
                    }

                    node.SudoCommand($"resize2fs {bootDisk}2", RunOptions.FaultOnError);
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

        /// <summary>
        /// Parses the NeonKUBE related tags from a virtual machine's notes.
        /// </summary>
        /// <param name="machine"></param>
        /// <returns>The dictionary of tags and values keyed by tag name.</returns>
        private Dictionary<string, string> ParseNoteTags(VirtualMachine machine)
        {
            Covenant.Requires<ArgumentNullException>(machine != null, nameof(machine));

            // Tags will look something like this:
            //
            //      neonkube
            //      neon-cluster-id: fe7f-7549-ab25-1583
            //      neon-node-name: control-0
            //      neonkube

            var tags = new Dictionary<string, string>();

            using (var reader = new StringReader(machine.Notes ?? string.Empty))
            {
                // We're going to ignore any lines before the first tag marker line
                // and then process tags up until the second marker line or the end
                // of the notes, ignoring anything that doesn't make sense.

                var seenFirstMarker = false;

                foreach (var line in reader.Lines())
                {
                    var isMarkerLine = line == tagMarkerLine;

                    if (!seenFirstMarker && isMarkerLine)
                    {
                        seenFirstMarker = true;
                        continue;
                    }
                    else if (isMarkerLine)
                    {
                        break;  // Must be the second marker line.
                    }

                    // Parse the tag line.

                    var colonPos = line.IndexOf(':');

                    if (colonPos == -1)
                    {
                        break;
                    }

                    var name  = line.Substring(0, colonPos).Trim();
                    var value = line.Substring(colonPos+1).Trim();

                    if (name.Length == 0 || value.Length == 0)
                    {
                        continue;   // Ignore invalid tags.
                    }

                    tags[name] = value;
                }
            }

            return tags;
        }

        /// <summary>
        /// Returns information about the cluster virtual machines and the cluster node names.
        /// </summary>
        /// <param name="hyperv">The Hyper-V proxy to use.</param>
        /// <returns>The cluster virtual machine information.</returns>
        private List<ClusterVm> GetClusterVms(HyperVProxy hyperv)
        {
            Covenant.Assert(cluster != null);

            // We're going to rely on the tags we encoded into the VM notes to
            // ensure that the VMs is associated with the current cluster and
            // also to obtain the node node name.

            var clusterVms = new List<ClusterVm>();
            var clusterId  = cluster.Id;

            // This can happen when there are no VMs deployed yet. 

            if (string.IsNullOrEmpty(clusterId))
            {
                return clusterVms;
            }

            foreach (var machine in hyperv.ListVms())
            {
                var tags = ParseNoteTags(machine);

                if (!tags.TryGetValue(clusterIdTag, out var machineClusterId) ||
                    !tags.TryGetValue(nodeNameTag, out var machineNodeName))
                {
                    continue;
                }

                if (machineNodeName != null && machineClusterId == clusterId)
                {
                    clusterVms.Add(new ClusterVm(machine, machineNodeName));
                }
            }

            return clusterVms;
        }

        /// <inheritdoc/>
        public override async Task<ClusterHealth> GetClusterHealthAsync(TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            // $todo(jefflill):
            //
            // We're using cluster name prefixes to identify Hyper-V virtual machines that
            // belong to the cluster.  This is a bit of a hack.
            //
            // We need to implement Hyper-V VM tagging in the future and then use a cluster
            // ID tag for this instead.
            //
            //      https://github.com/nforgeio/neonSDK/issues/67

            var clusterHealth = new ClusterHealth();

            using (var hyperv = new HyperVProxy())
            {
                if (timeout <= TimeSpan.Zero)
                {
                    timeout = DefaultStatusTimeout;
                }

                // Create a list of the Hyper-V virtual machines that belong to the cluster.

                var clusterVms = GetClusterVms(hyperv);

                // Update the cluster health state for the nodes from the virtual machine states.

                foreach (var clusterVm in clusterVms)
                {
                    var state = ClusterNodeState.NotProvisioned;

                    switch (clusterVm.Machine.State)
                    {
                        case VirtualMachineState.Unknown:

                            state = ClusterNodeState.Unknown;
                            break;

                        case VirtualMachineState.Off:

                            state = ClusterNodeState.Off;
                            break;

                        case VirtualMachineState.Starting:

                            state = ClusterNodeState.Starting;
                            break;

                        case VirtualMachineState.Running:

                            state = ClusterNodeState.Running;
                            break;

                        case VirtualMachineState.Paused:

                            state = ClusterNodeState.Unknown;
                            break;

                        case VirtualMachineState.Saved:

                            state = ClusterNodeState.Paused;
                            break;

                        default:

                            throw new NotImplementedException();
                    }

                    clusterHealth.Nodes.Add(clusterVm.NodeName, state);
                }

                // We're going to examine the node states from the Hyper-V perspective and
                // short-circuit the health check when the cluster nodes are not provisioned,
                // are paused or appear to be transitioning between states.

                if (clusterHealth.Nodes.Values.Count == 0)
                {
                    clusterHealth.State   = ClusterState.NotFound;
                    clusterHealth.Summary = "Cluster not found.";

                    return clusterHealth;
                }

                var commonNodeState = clusterHealth.Nodes.Values.First();

                foreach (var nodeState in clusterHealth.Nodes.Values)
                {
                    if (nodeState != commonNodeState)
                    {
                        // Nodes have differing states so we're going to consider the cluster
                        // to be transitioning.

                        clusterHealth.State   = ClusterState.Transitioning;
                        clusterHealth.Summary = "Cluster is transitioning";
                    }
                }

                if (cluster.SetupState != null && cluster.SetupState.DeploymentStatus != ClusterDeploymentStatus.Ready)
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
                            clusterHealth.Summary = "Cluster is running";
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
            }

            return clusterHealth;
        }

        /// <inheritdoc/>
        public override async Task StartClusterAsync()
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            // We just need to start any cluster VMs that aren't already running.

            using (var hyperv = new HyperVProxy())
            {
                var clusterVms = GetClusterVms(hyperv);

                Parallel.ForEach(clusterVms, parallelOptions,
                    clusterVm =>
                    {
                        var vm = clusterVm.Machine;

                        switch (vm.State)
                        {
                            case VirtualMachineState.Off:
                            case VirtualMachineState.Saved:

                                hyperv.StartVm(vm.Name);
                                break;

                            case VirtualMachineState.Running:
                            case VirtualMachineState.Starting:

                                break;

                            default:
                            case VirtualMachineState.Paused:
                            case VirtualMachineState.Unknown:

                                throw new NotImplementedException($"Unexpected VM state: {vm.Name}:{vm.State}");
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
                var clusterVms = GetClusterVms(hyperv);

                Parallel.ForEach(clusterVms, parallelOptions,
                    clusterVm =>
                    {
                        var vm = clusterVm.Machine;

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

                                        hyperv.SaveVm(vm.Name);
                                        break;

                                    case StopMode.Graceful:

                                        hyperv.StopVm(vm.Name);
                                        break;

                                    case StopMode.TurnOff:

                                        hyperv.StopVm(vm.Name, turnOff: true);
                                        break;
                                }
                                break;

                            default:
                            case VirtualMachineState.Unknown:

                                throw new NotImplementedException($"Unexpected VM state: {vm.Name}:{vm.State}");
                        }
                    });
            }
        }

        /// <inheritdoc/>
        public override async Task DeleteClusterAsync(ClusterDefinition clusterDefinition = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            using (var hyperv = new HyperVProxy())
            {
                Parallel.ForEach(GetClusterVms(hyperv), parallelOptions,
                    clusterVm =>
                    {
                        if (clusterVm.Machine.State == VirtualMachineState.Running || clusterVm.Machine.State == VirtualMachineState.Starting)
                        {
                            hyperv.StopVm(clusterVm.Machine.Name, turnOff: true);
                        }

                        hyperv.RemoveVm(clusterVm.Machine.Name);
                    });
            }
        }
    }
}
