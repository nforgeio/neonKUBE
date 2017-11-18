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
                                templateInfo.ETag = etags.SingleOrDefault();
                            }

                            File.WriteAllText(driveTemplateInfoPath, NeonHelper.JsonSerialize(templateInfo, Formatting.Indented));
                        }

                    }).Wait();

                controller.SetOperationStatus();
            }

            // Provision the VMs in Hyper-V.

            using (var hyperv = new HyperVClient())
            {
                var existingMachines = hyperv.ListVMs();
                var conflicts        = string.Empty;

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

                controller.SetOperationStatus(force ? "Stopping and/or removing existing VMs (--force)..." : "Checking for existing VMs");

                foreach (var machine in existingMachines)
                {
                    var drivePath     = Path.Combine(vmDriveFolder, $"{machine.Name}.vhdx");
                    var machineExists = cluster.FindNode(machine.Name) != null;

                    if (force)
                    {
                        if (machineExists)
                        {
                            if (machine.State != VirtualMachineState.Off)
                            {
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

                            if (machine.DrivePaths.Count != 1 || !machine.DrivePaths.First().Equals(drivePath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // Remove the machine and recreate it below.

                                hyperv.RemoveVM(machine.Name);
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    else if (machineExists)
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

                controller.SetOperationStatus();

                if (!string.IsNullOrEmpty(conflicts))
                {
                    throw new HyperVException($"[{conflicts}] virtual machine(s) already exist and connot be automatically replaced unless you specify [--force].");
                }

                // The cluster virtual machines are either stopped or don't exist at this point.
                // All we need to is to replace and existing hard drive with the template VHDX 
                // or create VMs that don't exist.

                foreach (var node in cluster.Definition.Nodes)
                {
                    controller.SetOperationStatus($"Configuring VM: [{node.Name}]");

                    // Extract the template file contents to the virtual machine's
                    // virtual hard drive file.

                    var drivePath = Path.Combine(vmDriveFolder, $"{node.Name}.vhdx");

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

                        using (var input = zip.GetInputStream(entry))
                        {
                            using (var output = new FileStream(drivePath, FileMode.Create, FileAccess.ReadWrite))
                            {
                                input.CopyTo(output);
                            }
                        }
                    }

                    if (!hyperv.VMExists(node.Name))
                    {
                        hyperv.AddVM(node.Name, memoryBytes: cluster.Definition.Hosting.Machine.VMMemory, drivePath: drivePath);
                    }

                    hyperv.StartVM(node.Name);
                }

                controller.SetOperationStatus();
            }
        }
    }
}
