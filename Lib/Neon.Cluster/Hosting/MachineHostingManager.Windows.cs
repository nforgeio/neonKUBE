//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.Windows.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Cluster.HyperV;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Cluster
{
    public partial class MachineHostingManager : HostingManager
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

        private string driveTemplatePath;
        private string vmDriveFolder;

        /// <summary>
        /// Handles the deployment of the cluster virtual machines on 
        /// Windows Hyper-V.
        /// </summary>
        /// <param name="force">Specifies whether any existing named VMs are to be stopped and overwritten.</param>
        private void DeployWindowsVMs(bool force)
        {
            // Determine where we're going to place the VM hard drive files and
            // ensure that the directory exists.

            if (!string.IsNullOrEmpty(cluster.Definition.Hosting.Machine.VMDriveFolder))
            {
                vmDriveFolder = cluster.Definition.Hosting.Machine.VMDriveFolder;
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

            var driveTemplateUri  = new Uri(cluster.Definition.Hosting.Machine.HostVhdxUri);
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
                controller.SetOperationStatus($"Downloading VHDX: [{cluster.Definition.Hosting.Machine.HostVhdxUri}]");

                Task.Run(
                    async () =>
                    {
                        using (var client = new HttpClient())
                        {
                            // Download the file.

                            var response = await client.GetAsync(cluster.Definition.Hosting.Machine.HostVhdxUri, HttpCompletionOption.ResponseHeadersRead);

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

                                                controller.SetOperationStatus($"Downloading VHDX: [{percentComplete}%] [{cluster.Definition.Hosting.Machine.HostVhdxUri}]");
                                            }
                                            else
                                            {
                                                controller.SetOperationStatus($"Downloading VHDX: [{fileStream.Length} bytes] [{cluster.Definition.Hosting.Machine.HostVhdxUri}]");
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

            // Provision the VMs in Hyper-V.

            using (var hyperv = new HyperVClient())
            {
                // We're going to create the [neonCLUSTER] external switch if it doesn't
                // already exist and attach the VMs to this switch.

                controller.SetOperationStatus("Scanning virtual switches");

                var switches   = hyperv.ListVMSwitches();
                var neonSwitch = switches.FirstOrDefault(s => s.Name.Equals("neonCLUSTER", StringComparison.InvariantCultureIgnoreCase));

                if (neonSwitch == null)
                {
                    hyperv.NewVMExternalSwitch("neonCLUSTER");
                }
                else if (neonSwitch.Type != VirtualSwitchType.External)
                {
                    throw new HyperVException($"Virtual switch [{neonSwitch.Name}] has type [{neonSwitch.Type}] which is not supported.  Change the switch type to [{VirtualSwitchType.External}].");
                }

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                controller.SetOperationStatus("Scanning virtual machines");

                var existingMachines = hyperv.ListVMs();
                var conflicts        = string.Empty;

                foreach (var machine in existingMachines)
                {
                    var drivePath   = Path.Combine(vmDriveFolder, $"{machine.Name}.vhdx");
                    var isClusterVM = cluster.FindNode(machine.Name) != null;

                    if (isClusterVM)
                    {
                        if (force)
                        {
                            if (machine.State != VirtualMachineState.Off)
                            {
                                controller.SetOperationStatus($"Stopping [{machine.Name}]");
                                hyperv.StopVM(machine.Name);
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

                                controller.SetOperationStatus($"Removing [{machine.Name}]");
                                hyperv.RemoveVM(machine.Name);
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

                            conflicts += machine.Name;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(conflicts))
                {
                    throw new HyperVException($"[{conflicts}] virtual machine(s) already exist and connot be automatically replaced unless you specify [--force].");
                }

                // The cluster virtual machines are either stopped or don't exist at this point.
                // All we need to is to replace any existing hard drive with the template VHDX 
                // or create VMs that don't exist.

                foreach (var nodeDefinition in cluster.Definition.Nodes)
                {
                    controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}]");

                    // Extract the template file contents to the virtual machine's
                    // virtual hard drive file.

                    var drivePath = Path.Combine(vmDriveFolder, $"{nodeDefinition.Name}.vhdx");

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
                            throw new ArgumentException($"[{driveTemplatePath}] ZIP archive includes entry [{entry.Name}] that's not a file.");
                        }

                        if (!entry.Name.EndsWith(".vhdx", StringComparison.InvariantCultureIgnoreCase))
                        {
                            throw new ArgumentException($"[{driveTemplatePath}] ZIP archive includes a file that's not named like [*.vhdx].");
                        }

                        controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}]: creating virtual drive: [{drivePath}]...");

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

                                    controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}]: [{percentComplete}%] creating virtual drive: [{drivePath}]...");
                                }
                            }
                        }
                    }

                    // Create the virtual machine if it doesn't already exist.

                    if (!hyperv.VMExists(nodeDefinition.Name))
                    {
                        controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}]: creating virtual machine");
                        hyperv.AddVM(nodeDefinition.Name, memoryBytes: cluster.Definition.Hosting.Machine.VMMemory, drivePath: drivePath, switchName: neonSwitch.Name);
                    }

                    controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}]: starting virtual machine");
                    hyperv.StartVM(nodeDefinition.Name);

                    // Retrive the virtual machine's network adapters (there should only be one) 
                    // to obtain the IP address we'll use to SSH into the machine and configure
                    // it's static IP.

                    controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}]: obtaining temporary IP address");

                    var adapters = hyperv.ListVMNetworkAdapters(nodeDefinition.Name, waitForAddresses: true);
                    var adapter  = adapters.FirstOrDefault();
                    var address  = adapter.Addresses.First();
                    var subnet   = NetworkCidr.Parse(cluster.Definition.Network.NodesSubnet);
                    var gateway  = cluster.Definition.Network.Gateway;

                    if (adapter == null)
                    {
                        throw new HyperVException($"Virtual machine [{nodeDefinition.Name}] has no network adapters.");
                    }

                    // We're going to temporarily set the node to the current VM address
                    // so we can connect via SSH.

                    var node        = cluster.GetNode(nodeDefinition.Name);
                    var nodeAddress = node.PrivateAddress;

                    try
                    {
                        node.PrivateAddress = address;

                        using (var nodeProxy = cluster.GetNode(nodeDefinition.Name))
                        {
                            controller.SetOperationStatus($"Connecting to [{nodeDefinition.Name}]");
                            nodeProxy.Connect();

                            // Replace the [/etc/network/interfaces] file to configure the static
                            // IP and then reboot to reinitialize networking subsystem.

                            controller.SetOperationStatus($"Configuring [{nodeDefinition.Name}] IP address [{nodeAddress}]");

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

                            var resolvBaseText =
$@"nameserver 8.8.8.8
nameserver 8.8.4.4
";
                            nodeProxy.UploadText("/etc/resolvconf/resolv.conf.d/base", resolvBaseText);

                            // Reboot to pick up the changes.

                            controller.SetOperationStatus($"Rebooting [{nodeDefinition.Name}]");
                            nodeProxy.Reboot(wait: false);
                        }
                    }
                    finally
                    {
                        // Restore the node's IP address.

                        node.PrivateAddress = nodeAddress;
                    }
                }

                controller.SetOperationStatus();

                // Recreate the node proxies because we disposed them above.
                // We need to do this so subsequent prepare steps will be
                // able to connect to the nodes via the correct addresses.

                cluster.CreateNodes();
            }
        }
    }
}
