//-----------------------------------------------------------------------------
// FILE:	    HyperVHostingManager.cs
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
using Neon.IO;
using Neon.Cluster.HyperV;
using Neon.Net;
using Neon.Time;

// $todo(jeff.lill):
//
// Extend this to support remote Hyper-V machines:
//
//      * Allow the specification of per-VM processors, memory, and disk size.
//      * I'm hoping I can use PowerShell to manage remote hosts (but what about OSX?).
//      * Including copying the VHDX files.

namespace Neon.Cluster
{
    /// <summary>
    /// Manages cluster provisioning on Microsoft Hyper-V virtual machines.
    /// </summary>
    public partial class HyperVHostingManager : HostingManager
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
        // Implementation

        private const string defaultSwitchName = "neonCLUSTER";

        private ClusterProxy                    cluster;
        private SetupController<NodeDefinition> controller;
        private bool                            forceVmOverwrite;
        private string                          driveTemplatePath;
        private string                          vmDriveFolder;
        private string                          switchName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public HyperVHostingManager(ClusterProxy cluster, string logFolder = null)
        {
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
        public override bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // $todo(jeff.lill):
            //
            // I'm not entirely sure that the [force] option makes sense for 
            // production clusters and especially when there are pet nodes.
            //
            // Perhaps it would make more sense to replace this with a
            // [neon cluster remove] command.
            //
            //      https://github.com/jefflill/NeonForge/issues/156

            this.forceVmOverwrite  = force;

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

            // If a public address isn't explicitly specified, we'll assume that the
            // tool is running inside the network and we can access the private address.

            foreach (var node in cluster.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.PublicAddress))
                {
                    node.PublicAddress = node.PrivateAddress;
                }
            }

            // Initialize and perform the provisioning operations.

            controller = new SetupController<NodeDefinition>($"Provisioning [{cluster.Definition.Name}] cluster", cluster.Nodes)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = 1     // We're only going to provision one VM at a time on a local Hyper-V instance.
            };

            controller.AddGlobalStep("prepare hyper-v", () => PrepareHyperV());
            controller.AddStep("create virtual machines", n => ProvisionVM(n));
            controller.AddGlobalStep(string.Empty, () => FinishHyperV(), quiet: true);

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
            return (Address: cluster.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
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
            // on-premise cluster deployments so we're going to return an 
            // empty list.

            return new List<HostedEndpoint>();
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => false;

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
            // Note that public endpoints have to be managed manually for
            // on-premise cluster deployments.
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine hosting the node.
        /// currently, this is the name of the cluster (capitalized) followed by a 
        /// dash and then the node name.  This convention will help disambiguate
        /// nodes from multiple clusters.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            return $"{cluster.Definition.Name.ToUpperInvariant()}-{node.Name}";
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
            var clusterPrefix = $"{cluster.Definition.Name.ToUpperInvariant()}-";

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

            if (!string.IsNullOrEmpty(cluster.Definition.Hosting.VmDriveFolder))
            {
                vmDriveFolder = cluster.Definition.Hosting.VmDriveFolder;
            }
            else
            {
                vmDriveFolder = @"C:\Users\Public\Documents\Hyper-V\Virtual Hard Disks\";
            }

            Directory.CreateDirectory(vmDriveFolder);

            // Download the zipped VHDX template if it's not already present or has 
            // changed.  Note that we're going to name the file the same as the file 
            // name from the URI and we're also going to persist the ETAG and file
            // length in file with the same name with a [.info] extension.
            //
            // Note that I'm not actually going check for ETAG changes to update
            // the download file.  The reason for this is that I want to avoid the
            // aituation where a user has provisioned some nodes with one version
            // of the template and then goes on later to provision new nodes with
            // an updated template.
            //
            // This should only be an issue for people using the default "latest"
            // drive template.  Production clusters should reference a specific
            // drive template.

            var driveTemplateUri  = new Uri(cluster.Definition.Hosting.HyperV.HostVhdxUri);
            var driveTemplateName = driveTemplateUri.Segments.Last();

            driveTemplatePath = Path.Combine(NeonClusterHelper.GetSetupFolder(), driveTemplateName);

            var driveTemplateInfoPath  = Path.Combine(NeonClusterHelper.GetSetupFolder(), driveTemplateName + ".info");
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
                controller.SetOperationStatus($"Download Template VHDX: [{cluster.Definition.Hosting.HyperV.HostVhdxUri}]");

                Task.Run(
                    async () =>
                    {
                        using (var client = new HttpClient())
                        {
                            // Download the file.

                            var response = await client.GetAsync(cluster.Definition.Hosting.HyperV.HostVhdxUri, HttpCompletionOption.ResponseHeadersRead);

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

                                                controller.SetOperationStatus($"Downloading VHDX: [{percentComplete}%] [{cluster.Definition.Hosting.HyperV.HostVhdxUri}]");
                                            }
                                            else
                                            {
                                                controller.SetOperationStatus($"Downloading VHDX: [{fileStream.Length} bytes] [{cluster.Definition.Hosting.HyperV.HostVhdxUri}]");
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
                // We're going to create the [neonCLUSTER] external switch if there
                // isn't already an external switch.

                controller.SetOperationStatus("Scanning virtual switches");

                var switches       = hyperv.ListVMSwitches();
                var externalSwitch = switches.FirstOrDefault(s => s.Type == VirtualSwitchType.External);

                if (externalSwitch == null)
                {
                    hyperv.NewVMExternalSwitch(switchName = defaultSwitchName);
                }
                else
                {
                    switchName = externalSwitch.Name;
                }

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                controller.SetOperationStatus("Scanning virtual machines");

                var existingMachines = hyperv.ListVMs();
                var conflicts = string.Empty;

                controller.SetOperationStatus("Stopping virtual machines");

                foreach (var machine in existingMachines)
                {
                    var nodeName    = ExtractNodeName(machine.Name);
                    var drivePath   = Path.Combine(vmDriveFolder, $"{machine.Name}.vhdx");
                    var isClusterVM = cluster.FindNode(nodeName) != null;

                    if (isClusterVM)
                    {
                        if (forceVmOverwrite)
                        {
                            if (machine.State != VirtualMachineState.Off)
                            {
                                cluster.GetNode(nodeName).Status = "stop virtual machine";
                                hyperv.StopVM(machine.Name);
                                cluster.GetNode(nodeName).Status = string.Empty;
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

                                cluster.GetNode(nodeName).Status = "delete virtual machine";
                                hyperv.RemoveVM(machine.Name);
                                cluster.GetNode(nodeName).Status = string.Empty;
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
        /// Creates a Hyper-V virtual machine for a cluster node.
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

                var drivePath = Path.Combine(vmDriveFolder, $"{vmName}.vhdx");

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

                    node.Status = $"create drive";

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
                                    node.Status = $"[{percentComplete}%] create drive";
                                    stopwatch.Restart();
                                }
                            }
                        }
                    }
                }

                // Create the virtual machine if it doesn't already exist.

                if (!hyperv.VMExists(vmName))
                {
                    node.Status = $"create virtual machine";
                    hyperv.AddVM(
                        vmName,
                        memorySize: cluster.Definition.Hosting.VmMemory,
                        minimumMemorySize: cluster.Definition.Hosting.VmMinimumMemory,
                        drivePath: drivePath,
                        switchName: switchName);
                }

                node.Status = $"start virtual machine";
                hyperv.StartVM(vmName);

                // Retrieve the virtual machine's network adapters (there should only be one) 
                // to obtain the IP address we'll use to SSH into the machine and configure
                // it's static IP.

                node.Status = $"get ip address";

                var adapters = hyperv.ListVMNetworkAdapters(vmName, waitForAddresses: true);
                var adapter  = adapters.FirstOrDefault();
                var address  = adapter.Addresses.First();
                var subnet   = NetworkCidr.Parse(cluster.Definition.Network.NodesSubnet);
                var gateway  = cluster.Definition.Network.Gateway;

                if (adapter == null)
                {
                    throw new HyperVException($"Virtual machine [{vmName}] has no network adapters.");
                }

                // We're going to temporarily set the node to the current VM address
                // so we can connect via SSH.

                var nodeAddress = node.PrivateAddress;

                try
                {
                    node.PrivateAddress = address;

                    using (var nodeProxy = cluster.GetNode(node.Name))
                    {
                        node.Status = $"connecting";
                        nodeProxy.Connect();

                        // Replace the [/etc/network/interfaces] file to configure the static
                        // IP and then reboot to reinitialize networking subsystem.

                        node.Status = $"set static ip [{nodeAddress}]";

                        var interfacesText =
$@"# This file describes the network interfaces available on your system
# and how to activate them. For more information, see interfaces(5).

source /etc/network/interfaces.d/*

# The loopback network interface
auto lo
iface lo inet loopback

# The primary network interface
auto eth0
iface eth0 inet static
address {nodeAddress}
netmask {subnet.Mask}
gateway {gateway}
broadcast {subnet.LastAddress}
";
                        nodeProxy.UploadText("/etc/network/interfaces", interfacesText);

                        // Temporarily configure the public Google DNS servers as
                        // the name servers so DNS will work after we reboot with
                        // the static IP.  Note that cluster setup will eventually
                        // configure the name servers specified in the cluster
                        // definition.

                        // $todo(jeff.lill):
                        //
                        // Is there a good reason why we're not just configuring the
                        // DNS servers from the cluster definition here???
                        //
                        // Using the Google DNS seems like it could break some cluster
                        // network configurations (i.e. for clusters that don't have
                        // access to the public Internet).  Totally private clusters
                        // aren't really a supported scenario right now though because
                        // we assume we can use [apt-get]... to pull down packages.

                        var resolvBaseText =
$@"nameserver 8.8.8.8
nameserver 8.8.4.4
";
                        nodeProxy.UploadText("/etc/resolvconf/resolv.conf.d/base", resolvBaseText);

                        // Reboot to pick up the changes.

                        node.Status = $"rebooting";
                        nodeProxy.Reboot(wait: false);
                    }
                }
                finally
                {
                    // Restore the node's IP address.

                    node.PrivateAddress = nodeAddress;
                }
            }
        }

        /// <summary>
        /// Perform any necessary global post Hyper-V provisioning steps.
        /// </summary>
        private void FinishHyperV()
        {
            // Recreate the node proxies because we disposed them above.
            // We need to do this so subsequent prepare steps will be
            // able to connect to the nodes via the correct addresses.

            cluster.CreateNodes();
        }
    }
}
