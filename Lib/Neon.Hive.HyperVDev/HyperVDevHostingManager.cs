//-----------------------------------------------------------------------------
// FILE:	    HyperVDevHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
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

using Neon.Common;
using Neon.Cryptography;
using Neon.HyperV;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Manages hive provisioning on the local workstation using Microsoft Hyper-V virtual machines.
    /// This is typically used for development and test purposes.
    /// </summary>
    [HostingProvider(HostingEnvironments.HyperVDev)]
    public class HyperVDevHostingManager : HostingManager
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
            [DefaultValue(null)]
            public string ETag { get; set; }

            /// <summary>
            /// The downloaded file length used as a quick verification that
            /// the complete file was downloaded.
            /// </summary>
            [JsonProperty(PropertyName = "Length", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
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
            // as a byproduct of calling this.
        }

        //---------------------------------------------------------------------
        // Instance members.

        private const string defaultSwitchName = "neonHIVE";

        private HiveProxy                       hive;
        private SetupController<NodeDefinition> controller;
        private bool                            forceVmOverwrite;
        private string                          driveTemplatePath;
        private string                          vmDriveFolder;
        private string                          switchName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hive">The hive being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public HyperVDevHostingManager(HiveProxy hive, string logFolder = null)
        {
            hive.HostingManager = this;

            this.hive = hive;
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
        public override void Validate(HiveDefinition hiveDefinition)
        {
            // Identify the OSD Bluestore block device for OSD nodes.

            if (hive.Definition.HiveFS.Enabled)
            {
                foreach (var node in hive.Definition.Nodes.Where(n => n.Labels.CephOSD))
                {
                    node.Labels.CephOSDDevice = "/dev/sdb";
                }
            }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // $todo(jeff.lill):
            //
            // I'm not entirely sure that the [force] option makes sense for 
            // production hives and especially when there are pet nodes.
            //
            // Perhaps it would make more sense to replace this with a
            // [neon hive remove] command.
            //
            //      https://github.com/jefflill/NeonForge/issues/156

            this.forceVmOverwrite  = force;

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in hive.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Labels.PhysicalMachine))
                {
                    node.Labels.PhysicalMachine = Environment.MachineName;
                }

                if (node.Labels.ComputeCores == 0)
                {
                    node.Labels.ComputeCores = hive.Definition.Hosting.VmProcessors;
                }

                if (node.Labels.ComputeRamMB == 0)
                {
                    node.Labels.ComputeRamMB = (int)(HiveDefinition.ValidateSize(hive.Definition.Hosting.VmMemory, typeof(HostingOptions), nameof(HostingOptions.VmMemory))/NeonHelper.Mega);
                }

                if (node.Labels.StorageCapacityGB == 0)
                {
                    node.Labels.StorageCapacityGB = (int)(node.GetVmMemory(hive.Definition) / NeonHelper.Giga);
                }
            }

            // If a public address isn't explicitly specified, we'll assume that the
            // tool is running inside the network and we can access the private address.

            foreach (var node in hive.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.PublicAddress))
                {
                    node.PublicAddress = node.PrivateAddress;
                }
            }

            // Perform the provisioning operations.

            controller = new SetupController<NodeDefinition>($"Provisioning [{hive.Definition.Name}] hive", hive.Nodes)
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
            return (Address: hive.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override void AddPostVpnSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override List<HostedEndpoint> GetPublicEndpoints()
        {
            // Note that public endpoints have to be managed manually for
            // on-premise hive deployments so we're going to return an 
            // empty list.

            return new List<HostedEndpoint>();
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => false;

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
            // Note that public endpoints have to be managed manually for
            // on-premise hive deployments.
        }

        /// <inheritdoc/>
        public override string DrivePrefix
        {
            get { return "sd"; }
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges
        {
            get { return true; }
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine hosting the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            return $"{hive.Definition.Hosting.GetVmNamePrefix(hive.Definition)}{node.Name}";
        }

        /// <summary>
        /// Attempts to extract the hive node name from a virtual machine name.
        /// </summary>
        /// <param name="machineName">The virtual machine name.</param>
        /// <returns>
        /// The extracted node name if the virtual machine belongs to this 
        /// hive or else the empty string.
        /// </returns>
        private string ExtractNodeName(string machineName)
        {
            var clusterPrefix = hive.Definition.Hosting.GetVmNamePrefix(hive.Definition);

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
        /// Performs any required Hyper-V initialization before host nodes can be provisioned.
        /// </summary>
        private void PrepareHyperV()
        {
            // Determine where we're going to place the VM hard drive files and
            // ensure that the directory exists.

            if (!string.IsNullOrEmpty(hive.Definition.Hosting.VmDriveFolder))
            {
                vmDriveFolder = hive.Definition.Hosting.VmDriveFolder;
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
            // an updated template.  The [neon hive setup --remove-templates] 
            // option is provided to delete any cached templates.
            //
            // This should only be an issue for people using the default "latest"
            // drive template.  Production hives should reference a specific
            // drive template.

            var driveTemplateUri  = new Uri(hive.Definition.Hosting.LocalHyperV.HostVhdxUri);
            var driveTemplateName = driveTemplateUri.Segments.Last();

            driveTemplatePath = Path.Combine(HiveHelper.GetVmTemplatesFolder(), driveTemplateName);

            var driveTemplateInfoPath  = Path.Combine(HiveHelper.GetVmTemplatesFolder(), driveTemplateName + ".info");
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
                controller.SetOperationStatus($"Download Template VHDX: [{hive.Definition.Hosting.LocalHyperV.HostVhdxUri}]");

                Task.Run(
                    async () =>
                    {
                        using (var client = new HttpClient())
                        {
                            // Download the file.

                            var response = await client.GetAsync(hive.Definition.Hosting.LocalHyperV.HostVhdxUri, HttpCompletionOption.ResponseHeadersRead);

                            response.EnsureSuccessStatusCode();

                            var contentLength = response.Content.Headers.ContentLength;

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

                                                controller.SetOperationStatus($"Downloading VHDX: [{percentComplete}%] [{hive.Definition.Hosting.LocalHyperV.HostVhdxUri}]");
                                            }
                                            else
                                            {
                                                controller.SetOperationStatus($"Downloading VHDX: [{fileStream.Length} bytes] [{hive.Definition.Hosting.LocalHyperV.HostVhdxUri}]");
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

                            templateInfo.Length = new FileInfo(driveTemplatePath).Length;

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
                // We're going to create the [neonHIVE] external switch if there
                // isn't already an external switch.

                controller.SetOperationStatus("Scanning network adapters");

                var switches       = hyperv.ListVMSwitches();
                var externalSwitch = switches.FirstOrDefault(s => s.Type == VirtualSwitchType.External);

                if (externalSwitch == null)
                {
                    hyperv.NewVMExternalSwitch(switchName = defaultSwitchName, IPAddress.Parse(hive.Definition.Network.Gateway));
                }
                else
                {
                    switchName = externalSwitch.Name;
                }

                // Ensure that the hive virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                controller.SetOperationStatus("Scanning virtual machines");

                var existingMachines = hyperv.ListVMs();
                var conflicts        = string.Empty;

                controller.SetOperationStatus("Stopping virtual machines");

                foreach (var machine in existingMachines)
                {
                    var nodeName    = ExtractNodeName(machine.Name);
                    var drivePath   = Path.Combine(vmDriveFolder, $"{machine.Name}.vhdx");
                    var isClusterVM = hive.FindNode(nodeName) != null;

                    if (isClusterVM)
                    {
                        if (forceVmOverwrite)
                        {
                            if (machine.State != VirtualMachineState.Off)
                            {
                                hive.GetNode(nodeName).Status = "stop virtual machine";
                                hyperv.StopVM(machine.Name);
                                hive.GetNode(nodeName).Status = string.Empty;
                            }

                            // The named machine already exists.  For force mode, we're going to stop and
                            // reuse the machine but replace the hard drive file as long as the file name
                            // matches what we're expecting for the machine.  We'll delete the VM if
                            // the names don't match and recreate it below.
                            //
                            // The reason for doing this is to avoid generating new MAC addresses
                            // every time we reprovision a VM.  This could help prevent the router/DHCP
                            // server from running out of IP addresses for the subnet.

                            var drives = hyperv.GetVMDrives(machine.Name);

                            if (drives.Count != 1 || !drives.First().Equals(drivePath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // Remove the machine and recreate it below.

                                hive.GetNode(nodeName).Status = "delete virtual machine";
                                hyperv.RemoveVM(machine.Name);
                                hive.GetNode(nodeName).Status = string.Empty;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            // We're going to report errors when one or more machines already exist and 
                            // [--force] was not specified.

                            if (conflicts.Length > 0)
                            {
                                conflicts += ", ";
                            }

                            conflicts += nodeName;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(conflicts))
                {
                    throw new HyperVException($"[{conflicts}] virtual machine(s) already exist and cannot be automatically replaced unless you specify [--force].");
                }

                controller.SetOperationStatus();
            }
        }

        /// <summary>
        /// Creates a Hyper-V virtual machine for a hive node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ProvisionVM(SshProxy<NodeDefinition> node)
        {
            // $todo(jeff.lill):
            //
            // This code currently assumes that the VM will use DHCP to obtain
            // its initial network configuration so the code can SSH into the
            // node to configure a static IP.
            //
            // It appears that it is possible to inject an IP address, but
            // I wasn't able to get this to work (perhaps Windows Server is
            // required.  Here's a link discussing this:
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

                // Extract the template file contents to the virtual machine's
                // virtual hard drive file.

                var drivePath = Path.Combine(vmDriveFolder, $"{vmName}-[0].vhdx");

                using (var zip = new ZipFile(driveTemplatePath))
                {
                    if (zip.Count != 1)
                    {
                        throw new ArgumentException($"[{driveTemplatePath}] ZIP archive includes more than one file.");
                    }

                    ZipEntry entry = null;

                    foreach (ZipEntry item in zip)
                    {
                        entry = item;
                        break;
                    }

                    if (!entry.IsFile)
                    {
                        throw new ArgumentException($"[{driveTemplatePath}] ZIP archive includes entry [{entry.Name}] that is not a file.");
                    }

                    if (!entry.Name.EndsWith(".vhdx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException($"[{driveTemplatePath}] ZIP archive includes a file that's not named like [*.vhdx].");
                    }

                    node.Status = $"create disk";

                    // $hack(jeff.lill): Update console at 2 sec intervals to avoid annoying flicker

                    var updateInterval = TimeSpan.FromSeconds(2);
                    var stopwatch      = new Stopwatch();

                    stopwatch.Start();

                    using (var input = zip.GetInputStream(entry))
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

                                var percentComplete = (int)(((double)output.Length / (double)entry.Size) * 100.0);

                                if (stopwatch.Elapsed >= updateInterval || percentComplete >= 100.0)
                                {
                                    node.Status = $"[{percentComplete}%] create disk";
                                    stopwatch.Restart();
                                }
                            }
                        }
                    }
                }

                // Stop and delete the virtual machine if one exists.

                if (hyperv.VMExists(vmName))
                {
                    hyperv.StopVM(vmName);
                    hyperv.RemoveVM(vmName);
                }

                // We need to create a raw drive if the node hosts a Ceph OSD.

                var extraDrives = new List<VirtualDrive>();

                if (node.Metadata.Labels.CephOSD)
                {
                    extraDrives.Add(
                        new VirtualDrive()
                        {
                            IsDynamic = true,
                            Size      = node.Metadata.GetCephOSDDriveSize(hive.Definition),
                            Path      = Path.Combine(vmDriveFolder, $"{vmName}-[1].vhdx")
                        });
                }

                // Create the virtual machine if it doesn't already exist.

                var processors     = node.Metadata.GetVmProcessors(hive.Definition);
                var memoryBytes    = node.Metadata.GetVmMemory(hive.Definition);
                var minMemoryBytes = node.Metadata.GetVmMinimumMemory(hive.Definition);
                var diskBytes      = node.Metadata.GetVmDisk(hive.Definition);

                node.Status = $"create virtual machine";
                hyperv.AddVM(
                    vmName,
                    processorCount: processors,
                    diskSize: diskBytes.ToString(),
                    memorySize: memoryBytes.ToString(),
                    minimumMemorySize: minMemoryBytes.ToString(),
                    drivePath: drivePath,
                    switchName: switchName,
                    extraDrives: extraDrives);

                node.Status = $"start virtual machine";

                hyperv.StartVM(vmName);

                // Retrieve the virtual machine's network adapters (there should only be one) 
                // to obtain the IP address we'll use to SSH into the machine and configure
                // it's static IP.

                node.Status = $"fetch ip address";

                var adapters  = hyperv.ListVMNetworkAdapters(vmName, waitForAddresses: true);
                var adapter   = adapters.FirstOrDefault();
                var address   = adapter.Addresses.First();
                var subnet    = NetworkCidr.Parse(hive.Definition.Network.PremiseSubnet);
                var gateway   = hive.Definition.Network.Gateway;
                var broadcast = hive.Definition.Network.Broadcast;

                if (adapter == null)
                {
                    throw new HyperVException($"Virtual machine [{vmName}] has no network adapters.");
                }

                // We're going to temporarily set the node to the current VM address
                // so we can connect via SSH.

                var savedNodeAddress = node.PrivateAddress;

                try
                {
                    node.PrivateAddress = address;

                    using (var nodeProxy = hive.GetNode(node.Name))
                    {
                        node.Status = $"connecting";
                        nodeProxy.WaitForBoot();

                        // We need to ensure that the host folders exist.

                        nodeProxy.CreateHiveHostFolders();

                        // Replace the [/etc/network/interfaces] file to configure the static
                        // IP and then reboot to reinitialize networking subsystem.

                        var primaryInterface = node.GetNetworkInterface(address);

                        node.Status = $"set static ip [{savedNodeAddress}]";

                        var interfacesText =
$@"# This file describes the network interfaces available on your system
# and how to activate them. For more information, see interfaces(5).

source /etc/network/interfaces.d/*

# The loopback network interface
auto lo
iface lo inet loopback

# The primary network interface
auto {primaryInterface}
iface {primaryInterface} inet static
address {savedNodeAddress}
netmask {subnet.Mask}
gateway {gateway}
broadcast {broadcast}
";
                        nodeProxy.UploadText("/etc/network/interfaces", interfacesText);

                        // Temporarily configure the public Google DNS servers as
                        // the name servers so DNS will work after we reboot with
                        // the static IP.  Note that hive setup will eventually
                        // configure the name servers specified in the hive
                        // definition.

                        // $todo(jeff.lill):
                        //
                        // Is there a good reason why we're not just configuring the
                        // DNS servers from the hive definition here???
                        //
                        // Using the Google DNS seems like it could break some hive
                        // network configurations (e.g. for hives that don't have
                        // access to the public Internet).  Totally private hives
                        // aren't really a supported scenario right now though because
                        // we assume we can use [apt-get]... to pull down packages.

                        var resolvBaseText =
$@"nameserver 8.8.8.8
nameserver 8.8.4.4
";
                        nodeProxy.UploadText("/etc/resolvconf/resolv.conf.d/base", resolvBaseText);

                        // Extend the primary partition and file system to fill 
                        // the virtual the drive. 

                        node.Status = $"resize primary partition";

                        // $hack(jeff.lill):
                        //
                        // I've seen a transient error here but can't reproduce it.  I'm going
                        // to assume for now that the file system might not be quite ready for
                        // this operation directly after the VM has been rebooted, so we're going
                        // to delay for a few seconds before performing the operations.

                        Thread.Sleep(TimeSpan.FromSeconds(5));
                        nodeProxy.SudoCommand("growpart /dev/sda 1");
                        nodeProxy.SudoCommand("resize2fs /dev/sda1");

                        // Reboot to pick up the changes.

                        node.Status = $"rebooting";
                        nodeProxy.Reboot(wait: false);
                    }
                }
                finally
                {
                    // Restore the node's IP address.

                    node.PrivateAddress = savedNodeAddress;
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

            hive.CreateNodes();
        }
    }
}
