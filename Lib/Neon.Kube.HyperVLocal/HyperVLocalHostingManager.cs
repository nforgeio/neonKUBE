//-----------------------------------------------------------------------------
// FILE:	    HyperVLocalHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using ICSharpCode.SharpZipLib.Zip;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.HyperV;
using Neon.IO;
using Neon.Net;
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
        // Private types

        /// <summary>
        /// Used to persist information about downloaded VHDX template files.
        /// </summary>
        public class DriveTemplateInfo
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

            /// <summary>
            /// Indicates whether the file is GZIP compressed.
            /// </summary>
            [JsonProperty(PropertyName = "Compressed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [YamlMember(Alias = "compressed", ApplyNamingConventions = false)]
            [DefaultValue(false)]
            public bool Compressed { get; set; }
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
        // Instance members.

        private const string defaultSwitchName = "external";

        private ClusterProxy                    cluster;
        private KubeSetupInfo                   setupInfo;
        private SetupController<NodeDefinition> setupController;
        private string                          driveTemplatePath;
        private string                          vmDriveFolder;
        private string                          switchName;
        private string                          secureSshPassword;

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
        /// <param name="cluster">The cluster being managed.
        /// </param>
        /// <param name="setupInfo">Specifies the cluster setup information.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public HyperVLocalHostingManager(ClusterProxy cluster, KubeSetupInfo setupInfo, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            cluster.HostingManager = this;

            this.cluster   = cluster;
            this.setupInfo = setupInfo;
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
        public override bool IsProvisionNOP
        {
            get { return false; }
        }

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
            Covenant.Assert(cluster != null, $"[{nameof(HyperVLocalHostingManager)}] was created with the wrong constructor.");

            this.secureSshPassword = secureSshPassword;

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

            // We'll call this to be consistent with the cloud hosting managers even though
            // the upstream on-premise router currently needs to be configured manually.

            KubeHelper.EnsureIngressNodes(cluster.Definition);

            // We need to ensure that at least one node will host the OpenEBS
            // cStore block device.

            KubeHelper.EnsureOpenEbsNodes(cluster.Definition);

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in cluster.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Labels.PhysicalMachine))
                {
                    node.Labels.PhysicalMachine = Environment.MachineName;
                }

                if (node.Labels.ComputeCores == 0)
                {
                    node.Labels.ComputeCores = cluster.Definition.Hosting.Vm.Processors;
                }

                if (node.Labels.ComputeRam == 0)
                {
                    node.Labels.ComputeRam = (int)(ClusterDefinition.ValidateSize(cluster.Definition.Hosting.Vm.Memory, typeof(HostingOptions), nameof(HostingOptions.Vm.Memory))/ ByteUnits.MebiBytes);
                }

                if (string.IsNullOrEmpty(node.Labels.StorageSize))
                {
                    node.Labels.StorageSize = ByteUnits.ToGiB(node.Vm.GetMemory(cluster.Definition));
                }
            }

            // Perform the provisioning operations.

            setupController = new SetupController<NodeDefinition>($"Provisioning [{cluster.Definition.Name}] cluster", cluster.Nodes)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = 1     // We're only going to provision one VM at a time on a local Hyper-V instance.
            };

            setupController.AddGlobalStep("prepare hyper-v", () => PrepareHyperV());
            setupController.AddNodeStep("create virtual machines", (node, stepDelay) => ProvisionVM(node));
            setupController.AddGlobalStep(string.Empty, () => Finish(), quiet: true);

            if (!setupController.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override void AddPostPrepareSteps(SetupController<NodeDefinition> setupController)
        {
            // We need to add any required OpenEBS cStore disks after the node has been otherwise
            // prepared.  We need to do this here because if we created the data and OpenEBS disks
            // when the VM is initially created, the disk setup scripts executed during prepare
            // won't be able to distinguish between the two disks.
            //
            // At this point, the data disk should be partitioned, formatted, and mounted so
            // the OpenEBS disk will be easy to identify as the only unpartitioned disk.

            setupController.AddNodeStep("openebs",
                (node, stepDelay) =>
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

                            node.Status = "openebs: add cstore disk";
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
                node => node.Metadata.OpenEBS);
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => true;

        /// <inheritdoc/>
        public override string GetDataDisk(SshProxy<NodeDefinition> node)
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
        private void PrepareHyperV()
        {
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

            // Download the GZIPed VHDX template if it's not already present or has 
            // changed.  Note that we're going to name the file the same as the file 
            // name from the URI and we're also going to persist the ETAG and file
            // length in file with the same name with a [.info] extension.
            //
            // Note that I'm not actually going check for ETAG changes to update
            // the download file.  The reason for this is that I want to avoid the
            // situation where the user has provisioned some nodes with one version
            // of the template and then goes on later to provision new nodes with
            // an updated template.
            //
            // This should only be an issue for people using the default "latest"
            // drive template.  Production clusters should reference a specific
            // drive template.

            var driveTemplateUri  = new Uri(setupInfo.LinuxTemplateUri);
            var driveTemplateName = driveTemplateUri.Segments.Last();

            driveTemplatePath = Path.Combine(KubeHelper.VmTemplatesFolder, driveTemplateName);

            var driveTemplateInfoPath  = Path.Combine(KubeHelper.VmTemplatesFolder, driveTemplateName + ".info");
            var driveTemplateIsCurrent = true;
            var driveTemplateInfo      = (DriveTemplateInfo)null;

            if (!File.Exists(driveTemplatePath) || !File.Exists(driveTemplateInfoPath))
            {
                driveTemplateIsCurrent = false;
            }
            else
            {
                try
                {
                    driveTemplateInfo = NeonHelper.JsonDeserialize<DriveTemplateInfo>(File.ReadAllText(driveTemplateInfoPath));

                    if (new FileInfo(driveTemplatePath).Length != driveTemplateInfo.Length)
                    {
                        driveTemplateIsCurrent = false;
                    }
                }
                catch
                {
                    // The [*.info] file must be corrupt.

                    driveTemplateIsCurrent = false;
                }
            }

            if (!driveTemplateIsCurrent)
            {
                setupController.SetOperationStatus($"Download Template VHDX: [{setupInfo.LinuxTemplateUri}]");

                Task.Run(
                    async () =>
                    {
                        using (var client = new HttpClient())
                        {
                            // Download the file.

                            var response = await client.GetAsync(setupInfo.LinuxTemplateUri, HttpCompletionOption.ResponseHeadersRead);

                            response.EnsureSuccessStatusCode();

                            var contentLength   = response.Content.Headers.ContentLength;
                            var contentEncoding = response.Content.Headers.ContentEncoding.SingleOrDefault();
                            var compressed      = false;

                            if (!string.IsNullOrEmpty(contentEncoding))
                            {
                                if (contentEncoding.Equals("gzip", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    compressed = true;
                                }
                                else
                                {
                                    throw new KubeException($"[{setupInfo.LinuxTemplateUri}] has unsupported [Content-Encoding={contentEncoding}].");
                                }
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

                                                setupController.SetOperationStatus($"Downloading VHDX: [{percentComplete}%] [{setupInfo.LinuxTemplateUri}]");
                                            }
                                            else
                                            {
                                                setupController.SetOperationStatus($"Downloading VHDX: [{fileStream.Length} bytes] [{setupInfo.LinuxTemplateUri}]");
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ensure that the template and info files are deleted if there were any
                                // errors, to help avoid using a corrupted template.

                                if (File.Exists(driveTemplatePath))
                                {
                                    File.Decrypt(driveTemplatePath);
                                }

                                if (File.Exists(driveTemplateInfoPath))
                                {
                                    File.Decrypt(driveTemplateInfoPath);
                                }

                                throw;
                            }

                            // Generate the [*.info] file.

                            var templateInfo = new DriveTemplateInfo();

                            templateInfo.Length     = new FileInfo(driveTemplatePath).Length;
                            templateInfo.Compressed = compressed;

                            if (response.Headers.TryGetValues("ETag", out var etags))
                            {
                                // Note that ETags look like they're surrounded by double
                                // quotes.  We're going to strip these out if present.

                                templateInfo.ETag = etags.SingleOrDefault().Replace("\"", string.Empty);
                            }

                            File.WriteAllText(driveTemplateInfoPath, NeonHelper.JsonSerialize(templateInfo, Formatting.Indented));
                        }

                    }).Wait();

                setupController.SetOperationStatus();
            }

            // Handle any necessary Hyper-V initialization.

            using (var hyperv = new HyperVClient())
            {
                // We're going to create an external Hyper-V switch if there
                // isn't already an external switch.

                setupController.SetOperationStatus("Scanning network adapters");

                var switches       = hyperv.ListVmSwitches();
                var externalSwitch = switches.FirstOrDefault(s => s.Type == VirtualSwitchType.External);

                if (externalSwitch == null)
                {
                    hyperv.NewVmExternalSwitch(switchName = defaultSwitchName, IPAddress.Parse(cluster.Definition.Network.Gateway));
                }
                else
                {
                    switchName = externalSwitch.Name;
                }

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                setupController.SetOperationStatus("Scanning virtual machines");

                var existingMachines = hyperv.ListVms();
                var conflicts        = string.Empty;

                setupController.SetOperationStatus("Stopping virtual machines");

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
                    throw new HyperVException($"[{conflicts}] virtual machine(s) already exist.");
                }

                setupController.SetOperationStatus();
            }
        }

        /// <summary>
        /// Creates a Hyper-V virtual machine for a cluster node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ProvisionVM(SshProxy<NodeDefinition> node)
        {
            using (var hyperv = new HyperVClient())
            {
                var vmName = GetVmName(node.Metadata);

                // $hack(jefflill): Update console at 2 sec intervals to mitigate annoying flicker

                var updateInterval = TimeSpan.FromSeconds(2);
                var stopwatch      = new Stopwatch();

                stopwatch.Start();

                // Copy the VHDX template file to the virtual machine's
                // virtual hard drive file.

                var driveTemplateInfoPath = driveTemplatePath + ".info";
                var driveTemplateInfo     = NeonHelper.JsonDeserialize<DriveTemplateInfo>(File.ReadAllText(driveTemplateInfoPath));
                var osDrivePath           = Path.Combine(vmDriveFolder, $"{vmName}.vhdx");

                node.Status = $"create: disk";

                using (var input = new FileStream(driveTemplatePath, FileMode.Open, FileAccess.Read))
                {
                    if (driveTemplateInfo.Compressed)
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
                                    cb = decompressor.Read(buffer, 0, buffer.Length);

                                    if (cb == 0)
                                    {
                                        break;
                                    }

                                    output.Write(buffer, 0, cb);

                                    cbRead += cb;

                                    var percentComplete = (int)(((double)output.Length / (double)cbRead) * 100.0);

                                    if (stopwatch.Elapsed >= updateInterval || percentComplete >= 100.0)
                                    {
                                        node.Status = $"create: disk [{percentComplete}%]";
                                        stopwatch.Restart();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var output = new FileStream(osDrivePath, FileMode.Create, FileAccess.ReadWrite))
                        {
                            var buffer = new byte[64 * 1024];
                            int cb;

                            while (true)
                            {
                                cb = input.Read(buffer, 0, buffer.Length);

                                if (cb == 0)
                                {
                                    break;
                                }

                                output.Write(buffer, 0, cb);

                                var percentComplete = (int)(((double)output.Length / (double)input.Length) * 100.0);

                                if (stopwatch.Elapsed >= updateInterval || percentComplete >= 100.0)
                                {
                                    node.Status = $"create: disk [{percentComplete}%]";
                                    stopwatch.Restart();
                                }
                            }
                        }
                    }
                }

                // Create the virtual machine.

                var processors       = node.Metadata.Vm.GetProcessors(cluster.Definition);
                var memoryBytes      = node.Metadata.Vm.GetMemory(cluster.Definition);
                var osDiskBytes      = node.Metadata.Vm.GetOsDisk(cluster.Definition);

                node.Status = $"create: virtual machine";
                hyperv.AddVm(
                    vmName,
                    processorCount: processors,
                    diskSize:       osDiskBytes.ToString(),
                    memorySize:     memoryBytes.ToString(),
                    drivePath:      osDrivePath,
                    switchName:     switchName);

                // Create a temporary ISO with the [neon-node-prep.sh] script, mount it
                // to the VM and then boot the VM for the first time.  The script on the
                // ISO will be executed automatically by the [neon-node-prep] service
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

                    node.Status = $"mount: neon-node-prep iso";
                    tempIso     = KubeHelper.CreateNodePrepIso(node.Cluster.Definition, node.Metadata, secureSshPassword);
                    hyperv.InsertVmDvd(vmName, tempIso.Path);

                    // Start the VM for the first time with the mounted ISO.  The network
                    // configuration will happen automatically by the time we can connect.

                    node.Status = $"start: virtual machine (first boot)";
                    hyperv.StartVm(vmName);

                    // Update the node credentials to use the secure password and then wait for the node to boot.

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUsername, secureSshPassword));

                    node.Status = $"connecting...";
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

                    if (osDiskBytes > KubeConst.NodeTemplateDiskSize)
                    {
                        node.Status = $"resize: OS disk";
                        node.SudoCommand($"growpart {osDisk} 2");
                        node.SudoCommand($"resize2fs {osDisk}2");
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
