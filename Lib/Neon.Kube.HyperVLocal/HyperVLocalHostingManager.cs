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
using Org.BouncyCastle.Utilities;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on the local workstation using Microsoft Hyper-V virtual machines.
    /// This is typically used for development and test purposes.
    /// </summary>
    [HostingProvider(HostingEnvironments.HyperVLocal)]
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
        private SetupController<NodeDefinition> controller;
        private string                          driveTemplatePath;
        private string                          vmDriveFolder;
        private string                          switchName;
        private string                          secureSshPassword;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
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
        }

        /// <inheritdoc/>
        public override bool Provision(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            this.secureSshPassword = secureSshPassword;

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

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

            controller = new SetupController<NodeDefinition>($"Provisioning [{cluster.Definition.Name}] cluster", cluster.Nodes)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = 1     // We're only going to provision one VM at a time on a local Hyper-V instance.
            };

            controller.AddGlobalStep("prepare hyper-v", () => PrepareHyperV());
            controller.AddStep("create virtual machines", (node, stepDelay) => ProvisionVM(node));
            controller.AddGlobalStep(string.Empty, () => Finish(), quiet: true);

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return false;
            }

            return true;
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

            if (!string.IsNullOrEmpty(cluster.Definition.Hosting.Vm.DriveFolder))
            {
                vmDriveFolder = cluster.Definition.Hosting.Vm.DriveFolder;
            }
            else
            {
                vmDriveFolder = HyperVClient.DefaultDriveFolder;
            }

            Directory.CreateDirectory(vmDriveFolder);

            // Download the zipped VHDX template if it's not already present or has 
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
                controller.SetOperationStatus($"Download Template VHDX: [{setupInfo.LinuxTemplateUri}]");

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

                                                controller.SetOperationStatus($"Downloading VHDX: [{percentComplete}%] [{setupInfo.LinuxTemplateUri}]");
                                            }
                                            else
                                            {
                                                controller.SetOperationStatus($"Downloading VHDX: [{fileStream.Length} bytes] [{setupInfo.LinuxTemplateUri}]");
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

                controller.SetOperationStatus();
            }

            // Handle any necessary Hyper-V initialization.

            using (var hyperv = new HyperVClient())
            {
                // We're going to create the [cluster] external switch if there
                // isn't already an external switch.

                controller.SetOperationStatus("Scanning network adapters");

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

                controller.SetOperationStatus("Scanning virtual machines");

                var existingMachines = hyperv.ListVms();
                var conflicts        = string.Empty;

                controller.SetOperationStatus("Stopping virtual machines");

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

                controller.SetOperationStatus();
            }
        }

        /// <summary>
        /// Creates a Hyper-V virtual machine for a cluster node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ProvisionVM(SshProxy<NodeDefinition> node)
        {
            // $todo(jefflill):
            //
            // This code currently assumes that the VM will use DHCP to obtain
            // its initial network configuration so the code can SSH into the
            // node to configure a static IP.
            //
            // It appears that it is possible to inject an IP address, but
            // I wasn't able to get this to work (perhaps Windows Server is
            // required).  Here's a link discussing this:
            //
            //  http://www.itprotoday.com/virtualization/modify-ip-configuration-vm-hyper-v-host
            //
            // An alternative technique might be to update [/etc/network/interfaces]
            // remotely via PowerShell as described here:
            //
            //  https://www.altaro.com/hyper-v/transfer-files-linux-hyper-v-guest/

            using (var hyperv = new HyperVClient())
            {
                var vmName = GetVmName(node.Metadata);

                // Copy the VHDX template file to the virtual machine's
                // virtual hard drive file.

                var driveTemplateInfoPath = driveTemplatePath + ".info";
                var driveTemplateInfo     = NeonHelper.JsonDeserialize<DriveTemplateInfo>(File.ReadAllText(driveTemplateInfoPath));
                var drivePath             = Path.Combine(vmDriveFolder, $"{vmName}.vhdx");

                node.Status = $"create: disk";

                // $hack(jefflill): Update console at 2 sec intervals to mitigate annoying flicker

                var updateInterval = TimeSpan.FromSeconds(2);
                var stopwatch      = new Stopwatch();

                stopwatch.Start();

                using (var input = new FileStream(driveTemplatePath, FileMode.Open, FileAccess.Read))
                {
                    if (driveTemplateInfo.Compressed)
                    {
                        using (var output = new FileStream(drivePath, FileMode.Create, FileAccess.ReadWrite))
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
                        using (var output = new FileStream(drivePath, FileMode.Create, FileAccess.ReadWrite))
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

                // Stop and delete the virtual machine if one already exists.

                if (hyperv.VmExists(vmName))
                {
                    hyperv.StopVm(vmName);
                    hyperv.RemoveVm(vmName);
                }

                // Create the virtual machine if it doesn't already exist.

                var processors  = node.Metadata.Vm.GetProcessors(cluster.Definition);
                var memoryBytes = node.Metadata.Vm.GetMemory(cluster.Definition);
                var diskBytes   = node.Metadata.Vm.GetDisk(cluster.Definition);

                node.Status = $"create: virtual machine";
                hyperv.AddVm(
                    vmName,
                    processorCount: processors,
                    diskSize:       diskBytes.ToString(),
                    memorySize:     memoryBytes.ToString(),
                    drivePath:      drivePath,
                    switchName:     switchName);

                // Create a temporary ISO with the [neon-node-prep.sh] script, mount it
                // to the VM and then boot the VM for the first time so that it will
                // pick up its network configuration.

                var tempIso = (TempFile)null;

                try
                {
                    using (var nodeProxy = cluster.GetNode(node.Name))
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
                        node.Status = $"connecting...";
                        nodeProxy.WaitForBoot();

                        // Extend the primary partition and file system to fill 
                        // the virtual drive.  Note that we're not going to do
                        // this if the specified drive size is less than or equal
                        // to the node template's drive size (because that
                        // would fail).

                        if (diskBytes > KubeConst.NodeTemplateDiskSize)
                        {
                            node.Status = $"resize: primary drive";

                            nodeProxy.SudoCommand("growpart /dev/sda 2");
                            nodeProxy.SudoCommand("resize2fs /dev/sda2");
                        }
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
            // Recreate the node proxies because we disposed them above.
            // We need to do this so subsequent prepare steps will be
            // able to connect to the nodes via the correct addresses.

            cluster.CreateNodes();
        }
    }
}
