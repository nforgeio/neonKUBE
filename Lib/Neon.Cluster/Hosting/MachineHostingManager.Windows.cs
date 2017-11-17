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
        private string vmDriveTemplatePath = NeonClusterHelper.DefaultVHDXPath;
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

            // Provision the VMs.

            using (var hyperv = new HyperVClient())
            {
                var existingMachines = hyperv.ListVMs();
                var conflicts        = string.Empty;

                // Ensure that the cluster virtual machines exist and are stopped,
                // taking care to issue a warning if any machines already exist 
                // and we're not doing [force] mode.

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

                if (!string.IsNullOrEmpty(conflicts))
                {
                    throw new HyperVException($"[{conflicts}] virtual machine(s) already exist and connot be automatically replaced unless you specify [--force].");
                }

                // The cluster virtual machines are either stopped or don't exist at this point.
                // All we need to is to replace and existing hard drive with the template VHDX 
                // or create VMs that don't exist.

                foreach (var node in cluster.Definition.Nodes)
                {
                    var drivePath = Path.Combine(vmDriveFolder, $"{node.Name}.vhdx");

                    File.Copy(vmDriveTemplatePath, drivePath);

                    if (!hyperv.FindVM(node.Name))
                    {
                        hyperv.AddVM(node.Name, memoryBytes: cluster.Definition.Hosting.Machine.VMMemory, drivePath: drivePath);
                    }

                    hyperv.StartVM(node.Name);
                }
            }
        }
    }
}
